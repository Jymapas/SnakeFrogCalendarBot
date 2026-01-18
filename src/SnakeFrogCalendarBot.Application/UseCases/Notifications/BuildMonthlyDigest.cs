using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Dto;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;
using AppClock = SnakeFrogCalendarBot.Application.Abstractions.Time.IClock;

namespace SnakeFrogCalendarBot.Application.UseCases.Notifications;

public sealed class BuildMonthlyDigest
{
    private readonly IEventRepository _eventRepository;
    private readonly IBirthdayRepository _birthdayRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly AppClock _clock;
    private readonly ITimeZoneProvider _timeZoneProvider;

    public BuildMonthlyDigest(
        IEventRepository eventRepository,
        IBirthdayRepository birthdayRepository,
        IAttachmentRepository attachmentRepository,
        AppClock clock,
        ITimeZoneProvider timeZoneProvider)
    {
        _eventRepository = eventRepository;
        _birthdayRepository = birthdayRepository;
        _attachmentRepository = attachmentRepository;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
    }

    public async Task<MonthlyDigestResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var nowInZone = Instant.FromDateTimeUtc(now).InZone(timeZone);
        var today = nowInZone.Date;

        var nextMonth = today.PlusMonths(1);
        var periodStart = new LocalDate(nextMonth.Year, nextMonth.Month, 1).AtMidnight();
        var lastDayOfMonth = CalendarSystem.Iso.GetDaysInMonth(nextMonth.Year, nextMonth.Month);
        var periodEnd = new LocalDate(nextMonth.Year, nextMonth.Month, lastDayOfMonth).At(LocalTime.MaxValue);

        var items = await BuildItemsAsync(periodStart, periodEnd, timeZone, cancellationToken);

        return new MonthlyDigestResult(periodStart.Date, periodEnd.Date, items, timeZone.Id);
    }

    private async Task<IReadOnlyList<CalendarItemDto>> BuildItemsAsync(
        LocalDateTime periodStart,
        LocalDateTime periodEnd,
        DateTimeZone timeZone,
        CancellationToken cancellationToken)
    {
        var items = new List<CalendarItemDto>();

        var allEvents = await _eventRepository.ListUpcomingAsync(cancellationToken);
        var allBirthdays = await _birthdayRepository.ListAsync(cancellationToken);

        var periodStartInstant = periodStart.InZoneLeniently(timeZone).ToInstant();
        var periodEndInstant = periodEnd.InZoneLeniently(timeZone).ToInstant();

        foreach (var eventEntity in allEvents)
        {
            LocalDate? eventDate = null;
            LocalTime? eventTime = null;

            if (eventEntity.Kind == EventKind.OneOff && eventEntity.OccursAtUtc.HasValue)
            {
                var instant = Instant.FromDateTimeOffset(eventEntity.OccursAtUtc.Value);
                var zonedDateTime = instant.InZone(timeZone);
                var localDateTime = zonedDateTime.LocalDateTime;
                eventDate = localDateTime.Date;
                eventTime = eventEntity.IsAllDay ? null : localDateTime.TimeOfDay;
            }
            else if (eventEntity.Kind == EventKind.Yearly && eventEntity.Month.HasValue && eventEntity.Day.HasValue)
            {
                var targetDate = new LocalDate(periodStart.Year, eventEntity.Month.Value, eventEntity.Day.Value);
                if (targetDate >= periodStart.Date && targetDate <= periodEnd.Date)
                {
                    eventDate = targetDate;
                    eventTime = eventEntity.IsAllDay ? null : (eventEntity.TimeOfDay.HasValue
                        ? LocalTime.FromTicksSinceMidnight(eventEntity.TimeOfDay.Value.Ticks)
                        : null);
                }
            }

            if (eventDate.HasValue)
            {
                var eventDateTime = eventTime.HasValue
                    ? eventDate.Value.At(eventTime.Value)
                    : eventDate.Value.AtMidnight();
                var eventInstant = eventDateTime.InZoneLeniently(timeZone).ToInstant();

                if (eventInstant >= periodStartInstant && eventInstant <= periodEndInstant)
                {
                    var attachment = await _attachmentRepository.GetCurrentByEventIdAsync(eventEntity.Id, cancellationToken);
                    items.Add(new CalendarItemDto
                    {
                        Date = eventDate.Value,
                        Time = eventTime,
                        Title = eventEntity.Title,
                        Type = CalendarItemType.Event,
                        IsAllDay = eventEntity.IsAllDay,
                        HasAttachment = attachment is not null
                    });
                }
            }
        }

        foreach (var birthday in allBirthdays)
        {
            var birthdayDate = new LocalDate(periodStart.Year, birthday.Month, birthday.Day);
            if (birthdayDate >= periodStart.Date && birthdayDate <= periodEnd.Date)
            {
                items.Add(new CalendarItemDto
                {
                    Date = birthdayDate,
                    Time = null,
                    Title = birthday.PersonName,
                    Type = CalendarItemType.Birthday,
                    IsAllDay = true,
                    HasAttachment = false,
                    BirthYear = birthday.BirthYear
                });
            }
        }

        return items.OrderBy(i => i.Date).ThenBy(i => i.Time ?? LocalTime.MaxValue).ToList();
    }
}

public sealed record MonthlyDigestResult(
    LocalDate PeriodStart,
    LocalDate PeriodEnd,
    IReadOnlyList<CalendarItemDto> Items,
    string TimeZoneId);