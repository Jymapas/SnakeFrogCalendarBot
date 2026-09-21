using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Dto;
using SnakeFrogCalendarBot.Application.Recurrence;

namespace SnakeFrogCalendarBot.Application.UseCases.Notifications;

public sealed class DigestItemsProvider
{
    private readonly IEventRepository _eventRepository;
    private readonly IBirthdayRepository _birthdayRepository;
    private readonly IAttachmentRepository _attachmentRepository;

    public DigestItemsProvider(
        IEventRepository eventRepository,
        IBirthdayRepository birthdayRepository,
        IAttachmentRepository attachmentRepository)
    {
        _eventRepository = eventRepository;
        _birthdayRepository = birthdayRepository;
        _attachmentRepository = attachmentRepository;
    }

    public async Task<IReadOnlyList<CalendarItemDto>> BuildAsync(
        LocalDate periodStart,
        LocalDate periodEnd,
        string timeZoneId,
        CancellationToken cancellationToken,
        LocalTime? nextDayLookaheadCutoff = null)
    {
        var timeZone = DateTimeZoneProviders.Tzdb[timeZoneId];
        var items = new List<CalendarItemDto>();

        var allEvents = await _eventRepository.ListAllAsync(cancellationToken);
        var allBirthdays = await _birthdayRepository.ListAsync(cancellationToken);

        foreach (var eventEntity in allEvents)
        {
            var occurrences = RecurrenceExpander.Expand(
                eventEntity,
                periodStart,
                periodEnd,
                timeZone,
                nextDayLookaheadCutoff);

            if (occurrences.Count == 0)
            {
                continue;
            }

            var attachments = await _attachmentRepository.GetByEventIdAsync(eventEntity.Id, cancellationToken);
            var currentAttachments = attachments.Where(attachment => attachment.IsCurrent).ToList();
            var attachmentsForDigest = currentAttachments.Count > 0
                ? currentAttachments
                : attachments.ToList();

            var attachmentDtos = attachmentsForDigest
                .Select(attachment => new DigestAttachmentDto(attachment.TelegramFileId, attachment.FileName))
                .ToList();

            foreach (var occurrence in occurrences)
            {
                items.Add(new CalendarItemDto
                {
                    Date = occurrence.Date,
                    Time = occurrence.Time,
                    Title = eventEntity.Title,
                    Type = CalendarItemType.Event,
                    IsAllDay = eventEntity.IsAllDay,
                    HasAttachment = attachmentDtos.Count > 0,
                    Attachments = attachmentDtos
                });
            }
        }

        foreach (var birthday in allBirthdays)
        {
            if (!RecurrenceExpander.TryResolve(birthday, periodStart, periodEnd, out var birthdayDate))
            {
                continue;
            }

            items.Add(new CalendarItemDto
            {
                Date = birthdayDate,
                Time = null,
                Title = birthday.PersonName,
                Type = CalendarItemType.Birthday,
                IsAllDay = true,
                HasAttachment = false,
                Attachments = [],
                BirthYear = birthday.BirthYear,
                Contact = birthday.Contact
            });
        }

        return items
            .OrderBy(item => item.Date)
            .ThenBy(item => item.Time ?? LocalTime.MaxValue)
            .ThenBy(item => item.Type)
            .ThenBy(item => item.Title)
            .ToList();
    }
}
