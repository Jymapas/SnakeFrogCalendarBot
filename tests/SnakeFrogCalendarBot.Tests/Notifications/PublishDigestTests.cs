using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Telegram;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Dto;
using SnakeFrogCalendarBot.Application.Formatting;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;
using AppClock = SnakeFrogCalendarBot.Application.Abstractions.Time.IClock;

namespace SnakeFrogCalendarBot.Tests.Notifications;

public sealed class PublishDigestTests
{
    [Test]
    public async Task PublishMonthlyAsync_PinsNewMessageWithoutNotification_AndUnpinsPrevious()
    {
        var telegramPublisher = new RecordingTelegramPublisher();
        var latestDigestPostRepository = new FakeLatestDigestPostRepository(
            existingMonthlyPost: new LatestDigestPostInfo(
                DigestType.Monthly,
                777,
                new DateTime(2026, 2, 1),
                new DateTime(2026, 2, 28, 23, 59, 59),
                "Europe/Moscow"));
        var notificationRunRepository = new FakeNotificationRunRepository();
        var publishDigest = CreatePublishDigest(
            telegramPublisher,
            latestDigestPostRepository,
            notificationRunRepository);

        var published = await publishDigest.PublishMonthlyAsync(new LocalDate(2026, 2, 28), CancellationToken.None);

        Assert.That(published, Is.True);
        Assert.That(telegramPublisher.SentTexts, Has.Count.EqualTo(1));
        Assert.That(telegramPublisher.PinnedMessages, Has.Count.EqualTo(1));
        Assert.That(telegramPublisher.PinnedMessages.Single().MessageId, Is.EqualTo(1001));
        Assert.That(telegramPublisher.PinnedMessages.Single().DisableNotification, Is.True);
        Assert.That(telegramPublisher.UnpinnedMessageIds, Is.EquivalentTo(new[] { 777 }));
    }

    [Test]
    public async Task PublishDailyAsync_DoesNotPinOrUnpinMessages()
    {
        var telegramPublisher = new RecordingTelegramPublisher();
        var latestDigestPostRepository = new FakeLatestDigestPostRepository();
        var notificationRunRepository = new FakeNotificationRunRepository();
        var publishDigest = CreatePublishDigest(
            telegramPublisher,
            latestDigestPostRepository,
            notificationRunRepository);

        var published = await publishDigest.PublishDailyAsync(new LocalDate(2026, 2, 28), CancellationToken.None);

        Assert.That(published, Is.True);
        Assert.That(telegramPublisher.PinnedMessages, Is.Empty);
        Assert.That(telegramPublisher.UnpinnedMessageIds, Is.Empty);
    }

    private static PublishDigest CreatePublishDigest(
        RecordingTelegramPublisher telegramPublisher,
        FakeLatestDigestPostRepository latestDigestPostRepository,
        FakeNotificationRunRepository notificationRunRepository)
    {
        var digestItemsProvider = new DigestItemsProvider(
            new FakeEventRepository(),
            new FakeBirthdayRepository(),
            new FakeAttachmentRepository());

        return new PublishDigest(
            digestItemsProvider,
            new DigestFormatter(),
            new SendDigest(telegramPublisher),
            notificationRunRepository,
            latestDigestPostRepository,
            telegramPublisher,
            new DigestPeriodCalculator(),
            new TestClock(new DateTime(2026, 2, 28, 12, 0, 0, DateTimeKind.Utc)),
            new TestTimeZoneProvider(),
            NullLogger<PublishDigest>.Instance);
    }

    private sealed class TestClock : AppClock
    {
        public TestClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }

    private sealed class TestTimeZoneProvider : ITimeZoneProvider
    {
        public string GetTimeZoneId() => "Europe/Moscow";
    }

    private sealed class FakeNotificationRunRepository : INotificationRunRepository
    {
        public Task AddAsync(NotificationRun notificationRun, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(
            DigestType digestType,
            DateTime periodStartLocal,
            DateTime periodEndLocal,
            string timeZoneId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class FakeLatestDigestPostRepository : ILatestDigestPostRepository
    {
        private readonly LatestDigestPostInfo? _existingMonthlyPost;

        public FakeLatestDigestPostRepository(LatestDigestPostInfo? existingMonthlyPost = null)
        {
            _existingMonthlyPost = existingMonthlyPost;
        }

        public Task UpsertAsync(
            DigestType digestType,
            int notificationRunId,
            int telegramMessageId,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<LatestDigestPostInfo?> GetByDigestTypeAsync(DigestType digestType, CancellationToken cancellationToken)
        {
            return Task.FromResult(digestType == DigestType.Monthly ? _existingMonthlyPost : null);
        }

        public Task<IReadOnlyList<LatestDigestPostInfo>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<LatestDigestPostInfo>>(
                _existingMonthlyPost is null ? [] : [_existingMonthlyPost]);
        }
    }

    private sealed class FakeEventRepository : IEventRepository
    {
        public Task AddAsync(Event eventEntity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Event>> ListUpcomingAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Event>>([]);
        public Task<IReadOnlyList<Event>> ListAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Event>>([]);
        public Task<IReadOnlyList<Event>> ListUpcomingForEditAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Event>>([]);
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
        public Task<IReadOnlyList<Attachment>> GetByEventIdAsync(int eventId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(Attachment attachment, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingTelegramPublisher : ITelegramPublisher
    {
        public List<string> SentTexts { get; } = [];
        public List<(int MessageId, bool DisableNotification)> PinnedMessages { get; } = [];
        public List<int> UnpinnedMessageIds { get; } = [];

        public Task<int> SendMessageAsync(string text, CancellationToken cancellationToken)
        {
            SentTexts.Add(text);
            return Task.FromResult(1001);
        }

        public Task EditMessageAsync(int messageId, string text, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task DeleteMessageAsync(int messageId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task PinMessageAsync(int messageId, bool disableNotification, CancellationToken cancellationToken)
        {
            PinnedMessages.Add((messageId, disableNotification));
            return Task.CompletedTask;
        }

        public Task UnpinMessageAsync(int messageId, CancellationToken cancellationToken)
        {
            UnpinnedMessageIds.Add(messageId);
            return Task.CompletedTask;
        }
    }
}
