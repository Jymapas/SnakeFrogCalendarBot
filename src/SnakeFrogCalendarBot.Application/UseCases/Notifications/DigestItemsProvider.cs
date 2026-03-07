using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Dto;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

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
        CancellationToken cancellationToken)
    {
        var timeZone = DateTimeZoneProviders.Tzdb[timeZoneId];
        var items = new List<CalendarItemDto>();

        var allEvents = await _eventRepository.ListAllAsync(cancellationToken);
        var allBirthdays = await _birthdayRepository.ListAsync(cancellationToken);

        foreach (var eventEntity in allEvents)
        {
            if (!DigestOccurrenceResolver.TryResolve(eventEntity, periodStart, periodEnd, timeZone, out var eventDate, out var eventTime))
            {
                continue;
            }

            var attachments = await _attachmentRepository.GetByEventIdAsync(eventEntity.Id, cancellationToken);
            var currentAttachments = attachments.Where(attachment => attachment.IsCurrent).ToList();
            var attachmentsForDigest = currentAttachments.Count > 0
                ? currentAttachments
                : attachments.ToList();

            items.Add(new CalendarItemDto
            {
                Date = eventDate,
                Time = eventTime,
                Title = eventEntity.Title,
                Type = CalendarItemType.Event,
                IsAllDay = eventEntity.IsAllDay,
                HasAttachment = attachmentsForDigest.Count > 0,
                Attachments = attachmentsForDigest
                    .Select(attachment => new DigestAttachmentDto(attachment.TelegramFileId, attachment.FileName))
                    .ToList()
            });
        }

        foreach (var birthday in allBirthdays)
        {
            if (!DigestOccurrenceResolver.TryResolve(birthday, periodStart, periodEnd, out var birthdayDate))
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

internal static class DigestOccurrenceResolver
{
    public static bool TryResolve(
        Event eventEntity,
        LocalDate periodStart,
        LocalDate periodEnd,
        DateTimeZone timeZone,
        out LocalDate date,
        out LocalTime? time)
    {
        date = default;
        time = null;

        if (eventEntity.Kind == EventKind.OneOff && eventEntity.OccursAtUtc.HasValue)
        {
            var occurrence = Instant.FromDateTimeOffset(eventEntity.OccursAtUtc.Value).InZone(timeZone).LocalDateTime;
            if (occurrence.Date < periodStart || occurrence.Date > periodEnd)
            {
                return false;
            }

            date = occurrence.Date;
            time = eventEntity.IsAllDay ? null : occurrence.TimeOfDay;
            return true;
        }

        if (eventEntity.Kind != EventKind.Yearly || !eventEntity.Month.HasValue || !eventEntity.Day.HasValue)
        {
            return false;
        }

        var targetDate = ResolveYearlyDate(periodStart, periodEnd, eventEntity.Month.Value, eventEntity.Day.Value);
        if (!targetDate.HasValue)
        {
            return false;
        }

        date = targetDate.Value;
        time = eventEntity.IsAllDay
            ? null
            : eventEntity.TimeOfDay.HasValue
                ? LocalTime.FromTicksSinceMidnight(eventEntity.TimeOfDay.Value.Ticks)
                : null;

        return true;
    }

    public static bool TryResolve(
        Birthday birthday,
        LocalDate periodStart,
        LocalDate periodEnd,
        out LocalDate date)
    {
        var targetDate = ResolveYearlyDate(periodStart, periodEnd, birthday.Month, birthday.Day);
        if (!targetDate.HasValue)
        {
            date = default;
            return false;
        }

        date = targetDate.Value;
        return true;
    }

    private static LocalDate? ResolveYearlyDate(LocalDate periodStart, LocalDate periodEnd, int month, int day)
    {
        var candidate = new LocalDate(periodStart.Year, month, day);
        if (candidate < periodStart)
        {
            candidate = candidate.PlusYears(1);
        }

        return candidate >= periodStart && candidate <= periodEnd
            ? candidate
            : null;
    }
}
