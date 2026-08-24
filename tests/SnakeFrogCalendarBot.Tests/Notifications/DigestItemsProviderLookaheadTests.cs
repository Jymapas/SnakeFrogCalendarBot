using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Tests.Notifications;

public sealed class DigestItemsProviderLookaheadTests
{
    private static readonly LocalDate Today = new(2026, 2, 28);
    private static readonly LocalTime Cutoff = DigestPeriodCalculator.DailyLookaheadCutoff;

    [Test]
    public async Task BuildAsync_WithLookaheadCutoff_IncludesOneOffEventBeforeCutoffNextDay()
    {
        var eventEntity = Event.CreateOneOff(
            "Ранний рейс",
            new DateTimeOffset(2026, 3, 1, 5, 0, 0, TimeSpan.FromHours(3)),
            false,
            null,
            null,
            null,
            new DateTime(2026, 2, 20, 9, 0, 0, DateTimeKind.Utc));

        var items = await BuildAsync([eventEntity], Cutoff);

        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].Date, Is.EqualTo(new LocalDate(2026, 3, 1)));
        Assert.That(items[0].Time, Is.EqualTo(new LocalTime(5, 0)));
    }

    [Test]
    public async Task BuildAsync_WithLookaheadCutoff_ExcludesOneOffEventAfterCutoffNextDay()
    {
        var eventEntity = Event.CreateOneOff(
            "Обед",
            new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.FromHours(3)),
            false,
            null,
            null,
            null,
            new DateTime(2026, 2, 20, 9, 0, 0, DateTimeKind.Utc));

        var items = await BuildAsync([eventEntity], Cutoff);

        Assert.That(items, Is.Empty);
    }

    [Test]
    public async Task BuildAsync_WithLookaheadCutoff_ExcludesAllDayEventNextDay()
    {
        var eventEntity = Event.CreateOneOff(
            "Праздник",
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.FromHours(3)),
            true,
            null,
            null,
            null,
            new DateTime(2026, 2, 20, 9, 0, 0, DateTimeKind.Utc));

        var items = await BuildAsync([eventEntity], Cutoff);

        Assert.That(items, Is.Empty);
    }

    [Test]
    public async Task BuildAsync_WithoutLookaheadCutoff_ExcludesEventOnNextDay()
    {
        var eventEntity = Event.CreateOneOff(
            "Ранний рейс",
            new DateTimeOffset(2026, 3, 1, 5, 0, 0, TimeSpan.FromHours(3)),
            false,
            null,
            null,
            null,
            new DateTime(2026, 2, 20, 9, 0, 0, DateTimeKind.Utc));

        var items = await BuildAsync([eventEntity], lookaheadCutoff: null);

        Assert.That(items, Is.Empty);
    }

    [Test]
    public async Task BuildAsync_WithLookaheadCutoff_IncludesYearlyEventBeforeCutoffNextDay()
    {
        var eventEntity = Event.CreateYearly(
            "Ежегодный запуск",
            3,
            1,
            new TimeSpan(6, 30, 0),
            false,
            null,
            null,
            null,
            new DateTime(2026, 2, 20, 9, 0, 0, DateTimeKind.Utc));

        var items = await BuildAsync([eventEntity], Cutoff);

        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].Date, Is.EqualTo(new LocalDate(2026, 3, 1)));
        Assert.That(items[0].Time, Is.EqualTo(new LocalTime(6, 30)));
    }

    private static async Task<IReadOnlyList<Application.Dto.CalendarItemDto>> BuildAsync(
        IReadOnlyList<Event> events,
        LocalTime? lookaheadCutoff)
    {
        var provider = new DigestItemsProvider(
            new FakeEventRepository(events),
            new FakeBirthdayRepository(),
            new FakeAttachmentRepository());

        return await provider.BuildAsync(Today, Today, "Europe/Moscow", CancellationToken.None, lookaheadCutoff);
    }

    private sealed class FakeEventRepository : IEventRepository
    {
        private readonly IReadOnlyList<Event> _events;

        public FakeEventRepository(IReadOnlyList<Event> events) => _events = events;

        public Task AddAsync(Event eventEntity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Event>> ListUpcomingAsync(CancellationToken cancellationToken) => Task.FromResult(_events);
        public Task<IReadOnlyList<Event>> ListAllAsync(CancellationToken cancellationToken) => Task.FromResult(_events);
        public Task<IReadOnlyList<Event>> ListUpcomingForEditAsync(CancellationToken cancellationToken) => Task.FromResult(_events);
        public Task<Event?> GetByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(Event eventEntity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeBirthdayRepository : IBirthdayRepository
    {
        public Task AddAsync(Birthday birthday, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Birthday>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Birthday>>([]);
        public Task<Birthday?> GetByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(Birthday birthday, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeAttachmentRepository : IAttachmentRepository
    {
        public Task AddAsync(Attachment attachment, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Attachment?> GetCurrentByEventIdAsync(int eventId, CancellationToken cancellationToken) => Task.FromResult<Attachment?>(null);
        public Task<Attachment?> GetCurrentByEventIdForUpdateAsync(int eventId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Attachment?> GetLatestByEventIdForUpdateAsync(int eventId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Attachment>> GetByEventIdAsync(int eventId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Attachment>>([]);
        public Task UpdateAsync(Attachment attachment, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
