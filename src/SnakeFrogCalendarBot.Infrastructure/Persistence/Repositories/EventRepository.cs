using Microsoft.EntityFrameworkCore;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;
using NodaTime;

namespace SnakeFrogCalendarBot.Infrastructure.Persistence.Repositories;

public sealed class EventRepository : IEventRepository
{
    private readonly CalendarDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ITimeZoneProvider _timeZoneProvider;

    public EventRepository(
        CalendarDbContext dbContext,
        IClock clock,
        ITimeZoneProvider timeZoneProvider)
    {
        _dbContext = dbContext;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
    }

    public async Task AddAsync(Event eventEntity, CancellationToken cancellationToken)
    {
        _dbContext.Events.Add(eventEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Event>> ListUpcomingAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var nowInZone = Instant.FromDateTimeUtc(now).InZone(timeZone);
        var today = nowInZone.Date;

        var oneOffEvents = await _dbContext.Events
            .AsNoTracking()
            .Where(e => e.Kind == EventKind.OneOff && e.OccursAtUtc.HasValue && e.OccursAtUtc.Value > now)
            .OrderBy(e => e.OccursAtUtc)
            .ToListAsync(cancellationToken);

        var yearlyEvents = await _dbContext.Events
            .AsNoTracking()
            .Where(e => e.Kind == EventKind.Yearly && e.Month.HasValue && e.Day.HasValue)
            .ToListAsync(cancellationToken);

        var yearlyWithNextOccurrence = yearlyEvents
            .Select(e =>
            {
                var thisYear = new LocalDate(today.Year, e.Month!.Value, e.Day!.Value);
                var nextOccurrence = thisYear >= today
                    ? thisYear
                    : thisYear.PlusYears(1);

                var localDateTime = e.IsAllDay
                    ? nextOccurrence.AtMidnight()
                    : nextOccurrence.At(e.TimeOfDay ?? LocalTime.Midnight);

                var zonedDateTime = localDateTime.InZoneLeniently(timeZone);
                var instant = zonedDateTime.ToInstant();
                var utcDateTime = instant.ToDateTimeUtc();

                return new { Event = e, NextOccurrence = utcDateTime };
            })
            .OrderBy(x => x.NextOccurrence)
            .Select(x => x.Event)
            .ToList();

        var allEvents = oneOffEvents
            .Concat(yearlyWithNextOccurrence)
            .OrderBy(e =>
            {
                if (e.Kind == EventKind.OneOff && e.OccursAtUtc.HasValue)
                {
                    return e.OccursAtUtc.Value;
                }

                var thisYear = new LocalDate(today.Year, e.Month!.Value, e.Day!.Value);
                var nextOccurrence = thisYear >= today
                    ? thisYear
                    : thisYear.PlusYears(1);

                var localDateTime = e.IsAllDay
                    ? nextOccurrence.AtMidnight()
                    : nextOccurrence.At(e.TimeOfDay ?? LocalTime.Midnight);

                var zonedDateTime = localDateTime.InZoneLeniently(timeZone);
                var instant = zonedDateTime.ToInstant();
                return instant.ToDateTimeUtc();
            })
            .ToList();

        return allEvents;
    }
}