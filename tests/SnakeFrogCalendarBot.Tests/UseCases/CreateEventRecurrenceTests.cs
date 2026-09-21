using Microsoft.Extensions.Logging.Abstractions;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Telegram;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Dto;
using SnakeFrogCalendarBot.Application.Formatting;
using SnakeFrogCalendarBot.Application.UseCases.Events;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Tests.UseCases;

public sealed class CreateEventRecurrenceTests
{
    private const int MondayAndWednesday = 0b0000101;

    [Test]
    public async Task ExecuteAsync_WeeklyCommand_CreatesWeeklyEvent()
    {
        var (useCase, repository) = CreateUseCase();

        await useCase.ExecuteAsync(
            new CreateEventCommand
            {
                Title = "Тренировка",
                Kind = EventKind.Weekly,
                IsAllDay = false,
                TimeOfDay = new TimeSpan(19, 0, 0),
                DaysOfWeekMask = MondayAndWednesday,
                StartDateLocal = new DateOnly(2026, 3, 2)
            },
            CancellationToken.None);

        var created = repository.Added.Single();
        Assert.Multiple(() =>
        {
            Assert.That(created.Kind, Is.EqualTo(EventKind.Weekly));
            Assert.That(created.DaysOfWeekMask, Is.EqualTo(MondayAndWednesday));
            Assert.That(created.StartDateLocal, Is.EqualTo(new DateOnly(2026, 3, 2)));
            Assert.That(created.TimeOfDay, Is.EqualTo(new TimeSpan(19, 0, 0)));
        });
    }

    [Test]
    public async Task ExecuteAsync_IntervalCommand_CreatesIntervalEvent()
    {
        var (useCase, repository) = CreateUseCase();

        await useCase.ExecuteAsync(
            new CreateEventCommand
            {
                Title = "Оплата счёта",
                Kind = EventKind.Interval,
                IsAllDay = true,
                IntervalCount = 2,
                IntervalUnit = RecurrenceUnit.Months,
                StartDateLocal = new DateOnly(2026, 3, 2),
                RepeatUntilLocal = new DateOnly(2026, 12, 31)
            },
            CancellationToken.None);

        var created = repository.Added.Single();
        Assert.Multiple(() =>
        {
            Assert.That(created.Kind, Is.EqualTo(EventKind.Interval));
            Assert.That(created.IntervalCount, Is.EqualTo(2));
            Assert.That(created.IntervalUnit, Is.EqualTo(RecurrenceUnit.Months));
            Assert.That(created.RepeatUntilLocal, Is.EqualTo(new DateOnly(2026, 12, 31)));
        });
    }

    [Test]
    public void ExecuteAsync_WeeklyCommandWithoutStartDate_Throws()
    {
        var (useCase, _) = CreateUseCase();

        Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new CreateEventCommand
            {
                Title = "Тренировка",
                Kind = EventKind.Weekly,
                IsAllDay = true,
                DaysOfWeekMask = MondayAndWednesday
            },
            CancellationToken.None));
    }

    private static (CreateEvent UseCase, RecordingEventRepository Repository) CreateUseCase()
    {
        var repository = new RecordingEventRepository();
        var refresh = new RefreshLatestDigestPosts(
            new EmptyLatestDigestPostRepository(),
            new DigestItemsProvider(repository, new EmptyBirthdayRepository(), new EmptyAttachmentRepository()),
            new DigestFormatter(),
            new NoopTelegramPublisher());

        var useCase = new CreateEvent(
            repository,
            new TestClock(),
            new TestTimeZoneProvider(),
            refresh,
            NullLogger<CreateEvent>.Instance);

        return (useCase, repository);
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow => new(2026, 2, 20, 9, 0, 0, DateTimeKind.Utc);
    }

    private sealed class TestTimeZoneProvider : ITimeZoneProvider
    {
        public string GetTimeZoneId() => "Europe/Moscow";
    }

    private sealed class RecordingEventRepository : IEventRepository
    {
        public List<Event> Added { get; } = [];

        public Task AddAsync(Event eventEntity, CancellationToken cancellationToken)
        {
            Added.Add(eventEntity);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Event>> ListUpcomingAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Event>> ListAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Event>>(Added);
        public Task<IReadOnlyList<Event>> ListUpcomingForEditAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Event?> GetByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(Event eventEntity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class EmptyBirthdayRepository : IBirthdayRepository
    {
        public Task AddAsync(Birthday birthday, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Birthday>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Birthday>>([]);
        public Task<Birthday?> GetByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(Birthday birthday, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class EmptyAttachmentRepository : IAttachmentRepository
    {
        public Task AddAsync(Attachment attachment, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Attachment?> GetCurrentByEventIdAsync(int eventId, CancellationToken cancellationToken) => Task.FromResult<Attachment?>(null);
        public Task<Attachment?> GetCurrentByEventIdForUpdateAsync(int eventId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Attachment?> GetLatestByEventIdForUpdateAsync(int eventId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Attachment>> GetByEventIdAsync(int eventId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Attachment>>([]);
        public Task UpdateAsync(Attachment attachment, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class EmptyLatestDigestPostRepository : ILatestDigestPostRepository
    {
        public Task<IReadOnlyList<LatestDigestPostInfo>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<LatestDigestPostInfo>>([]);

        public Task UpsertAsync(DigestType digestType, int notificationRunId, int telegramMessageId, DateTime updatedAtUtc, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<LatestDigestPostInfo?> GetByDigestTypeAsync(DigestType digestType, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class NoopTelegramPublisher : ITelegramPublisher
    {
        public Task<int> SendMessageAsync(string text, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task EditMessageAsync(int messageId, string text, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendDocumentAsync(string fileId, string caption, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteMessageAsync(int messageId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task PinMessageAsync(int messageId, bool disableNotification, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UnpinMessageAsync(int messageId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
