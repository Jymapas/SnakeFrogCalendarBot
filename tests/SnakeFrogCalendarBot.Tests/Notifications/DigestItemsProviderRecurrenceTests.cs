using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Dto;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Tests.Notifications;

public sealed class DigestItemsProviderRecurrenceTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 2, 20, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly WeeklyStart = new(2026, 3, 2);

    private const int MondayAndWednesday = 0b0000101;

    [Test]
    public async Task BuildAsync_WeeklyEventInWeek_ProducesItemPerOccurrence()
    {
        var eventEntity = Event.CreateWeekly(
            "Тренировка", MondayAndWednesday, WeeklyStart, new TimeSpan(19, 0, 0), false, null,
            null, null, null, CreatedAtUtc);

        var (items, _) = await BuildAsync([eventEntity], new LocalDate(2026, 3, 2), new LocalDate(2026, 3, 8));

        Assert.That(items, Has.Count.EqualTo(2));
        Assert.That(items.Select(item => item.Date), Is.EqualTo(new[]
        {
            new LocalDate(2026, 3, 2),
            new LocalDate(2026, 3, 4)
        }));
        Assert.That(items.All(item => item.Title == "Тренировка"), Is.True);
        Assert.That(items.All(item => item.Time == new LocalTime(19, 0)), Is.True);
    }

    [Test]
    public async Task BuildAsync_RecurringEvent_QueriesAttachmentsOncePerEvent()
    {
        var eventEntity = Event.CreateWeekly(
            "Тренировка", MondayAndWednesday, WeeklyStart, null, true, null,
            null, null, null, CreatedAtUtc);

        var (items, attachmentQueries) = await BuildAsync([eventEntity], new LocalDate(2026, 3, 1), new LocalDate(2026, 3, 31));

        Assert.That(items, Has.Count.EqualTo(9));
        Assert.That(attachmentQueries, Is.EqualTo(1));
    }

    [Test]
    public async Task BuildAsync_IntervalEvent_IsSortedTogetherWithOtherItems()
    {
        var weekly = Event.CreateWeekly(
            "Тренировка", 0b0000001, WeeklyStart, new TimeSpan(19, 0, 0), false, null,
            null, null, null, CreatedAtUtc);
        var interval = Event.CreateInterval(
            "Оплата счёта", 2, RecurrenceUnit.Weeks, new DateOnly(2026, 3, 3), null, true, null,
            null, null, null, CreatedAtUtc);

        var (items, _) = await BuildAsync([weekly, interval], new LocalDate(2026, 3, 2), new LocalDate(2026, 3, 15));

        Assert.That(items.Select(item => (item.Date.Day, item.Title)), Is.EqualTo(new[]
        {
            (2, "Тренировка"),
            (3, "Оплата счёта"),
            (9, "Тренировка")
        }));
    }

    [Test]
    public async Task BuildAsync_RecurringEventOutsideItsBoundaries_IsSkipped()
    {
        var eventEntity = Event.CreateWeekly(
            "Тренировка", MondayAndWednesday, WeeklyStart, null, true, new DateOnly(2026, 3, 8),
            null, null, null, CreatedAtUtc);

        var (items, attachmentQueries) = await BuildAsync([eventEntity], new LocalDate(2026, 4, 1), new LocalDate(2026, 4, 30));

        Assert.That(items, Is.Empty);
        Assert.That(attachmentQueries, Is.Zero);
    }

    private static async Task<(IReadOnlyList<CalendarItemDto> Items, int AttachmentQueries)> BuildAsync(
        IReadOnlyList<Event> events,
        LocalDate periodStart,
        LocalDate periodEnd)
    {
        var attachmentRepository = new CountingAttachmentRepository();
        var provider = new DigestItemsProvider(
            new FakeEventRepository(events),
            new FakeBirthdayRepository(),
            attachmentRepository);

        var items = await provider.BuildAsync(periodStart, periodEnd, "Europe/Moscow", CancellationToken.None);
        return (items, attachmentRepository.QueryCount);
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

    private sealed class CountingAttachmentRepository : IAttachmentRepository
    {
        public int QueryCount { get; private set; }

        public Task AddAsync(Attachment attachment, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Attachment?> GetCurrentByEventIdAsync(int eventId, CancellationToken cancellationToken) => Task.FromResult<Attachment?>(null);
        public Task<Attachment?> GetCurrentByEventIdForUpdateAsync(int eventId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Attachment?> GetLatestByEventIdForUpdateAsync(int eventId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<Attachment>> GetByEventIdAsync(int eventId, CancellationToken cancellationToken)
        {
            QueryCount++;
            return Task.FromResult<IReadOnlyList<Attachment>>([]);
        }

        public Task UpdateAsync(Attachment attachment, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
