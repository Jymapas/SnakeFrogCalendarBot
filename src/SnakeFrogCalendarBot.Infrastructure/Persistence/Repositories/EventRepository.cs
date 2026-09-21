using Microsoft.EntityFrameworkCore;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Recurrence;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;
using NodaTime;
using IClock = SnakeFrogCalendarBot.Application.Abstractions.Time.IClock;

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
        var now = Instant.FromDateTimeUtc(_clock.UtcNow);

        // Разовое событие, уже прошедшее сегодня, в «ближайшие» не попадает.
        return await ListOrderedByNextOccurrenceAsync(
            (eventEntity, next) => eventEntity.Kind != EventKind.OneOff || next > now,
            cancellationToken);
    }

    public async Task<Event?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _dbContext.Events
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Event eventEntity, CancellationToken cancellationToken)
    {
        _dbContext.Events.Update(eventEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var eventEntity = await _dbContext.Events
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (eventEntity is not null)
        {
            _dbContext.Events.Remove(eventEntity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<Event>> ListAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Events
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Event>> ListUpcomingForEditAsync(CancellationToken cancellationToken)
    {
        return await ListOrderedByNextOccurrenceAsync(null, cancellationToken);
    }

    /// <summary>
    /// Отбирает события, у которых есть вхождение начиная с сегодняшнего дня, и сортирует их
    /// по ближайшему вхождению. Расчёт общий для всех типов повторения, поэтому новые типы
    /// подхватываются автоматически.
    /// </summary>
    private async Task<IReadOnlyList<Event>> ListOrderedByNextOccurrenceAsync(
        Func<Event, Instant, bool>? filter,
        CancellationToken cancellationToken)
    {
        var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var today = Instant.FromDateTimeUtc(_clock.UtcNow).InZone(timeZone).Date;

        var allEvents = await _dbContext.Events
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return allEvents
            .Select(eventEntity => new
            {
                Event = eventEntity,
                NextOccurrence = EventNextOccurrenceCalculator.TryGetNextOccurrence(eventEntity, today, timeZone)
            })
            .Where(x => x.NextOccurrence.HasValue)
            .Where(x => filter is null || filter(x.Event, x.NextOccurrence!.Value))
            .OrderBy(x => x.NextOccurrence!.Value)
            .Select(x => x.Event)
            .ToList();
    }
}
