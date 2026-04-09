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

    [Test]
    public async Task PublishDailyAsync_SendsEventAttachments()
    {
        var telegramPublisher = new RecordingTelegramPublisher();
        var latestDigestPostRepository = new FakeLatestDigestPostRepository();
        var notificationRunRepository = new FakeNotificationRunRepository();
        var eventEntity = Event.CreateOneOff(
            "Презентация",
            new DateTimeOffset(2026, 2, 28, 10, 0, 0, TimeSpan.FromHours(3)),
            false,
            null,
            null,
            null,
            new DateTime(2026, 2, 20, 9, 0, 0, DateTimeKind.Utc));
        var attachments =
            new Attachment(
                1,
                "telegram-file-id-1",
                "telegram-file-unique-id-1",
                "agenda.pdf",
                "application/pdf",
                1024,
                1,
                true,
                new DateTime(2026, 2, 20, 9, 1, 0, DateTimeKind.Utc));

        var publishDigest = CreatePublishDigest(
            telegramPublisher,
            latestDigestPostRepository,
            notificationRunRepository,
            events: [eventEntity],
            attachments: [attachments]);

        var published = await publishDigest.PublishDailyAsync(new LocalDate(2026, 2, 28), CancellationToken.None);

        Assert.That(published, Is.True);
        Assert.That(telegramPublisher.SentDocuments, Is.EquivalentTo(new[] { ("telegram-file-id-1", "agenda.pdf") }));
        Assert.That(telegramPublisher.PinnedMessages, Is.Empty);
        Assert.That(telegramPublisher.UnpinnedMessageIds, Is.Empty);
    }

    [Test]
    public async Task PublishDailyAsync_WhenAttachmentSendFails_StillPublishesDigest()
    {
        var telegramPublisher = new RecordingTelegramPublisher
        {
            ThrowOnSendDocument = true
        };
        var latestDigestPostRepository = new FakeLatestDigestPostRepository();
        var notificationRunRepository = new FakeNotificationRunRepository();
        var eventEntity = Event.CreateOneOff(
            "Встреча",
            new DateTimeOffset(2026, 2, 28, 11, 0, 0, TimeSpan.FromHours(3)),
            false,
            null,
            null,
            null,
            new DateTime(2026, 2, 20, 9, 0, 0, DateTimeKind.Utc));
        var attachments =
            new Attachment(
                1,
                "telegram-file-id-2",
                "telegram-file-unique-id-2",
                "brief.pdf",
                "application/pdf",
                1024,
                1,
                true,
                new DateTime(2026, 2, 20, 9, 1, 0, DateTimeKind.Utc));
        var publishDigest = CreatePublishDigest(
            telegramPublisher,
            latestDigestPostRepository,
            notificationRunRepository,
            events: [eventEntity],
            attachments: [attachments]);

        var published = await publishDigest.PublishDailyAsync(new LocalDate(2026, 2, 28), CancellationToken.None);

        Assert.That(published, Is.True);
        Assert.That(telegramPublisher.SentTexts, Has.Count.EqualTo(1));
    }

    private static PublishDigest CreatePublishDigest(
        RecordingTelegramPublisher telegramPublisher,
        FakeLatestDigestPostRepository latestDigestPostRepository,
        FakeNotificationRunRepository notificationRunRepository,
        IReadOnlyList<Event>? events = null,
        IReadOnlyList<Attachment>? attachments = null)
    {
        var digestItemsProvider = new DigestItemsProvider(
            new FakeEventRepository(events ?? []),
            new FakeBirthdayRepository(),
            new FakeAttachmentRepository(attachments ?? []));

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
        private readonly IReadOnlyList<Event> _events;

        public FakeEventRepository(IReadOnlyList<Event> events)
        {
            _events = events;
        }

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
        private readonly IReadOnlyList<Attachment> _attachments;

        public FakeAttachmentRepository(IReadOnlyList<Attachment> attachments)
        {
            _attachments = attachments;
        }

        public Task AddAsync(Attachment attachment, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Attachment?> GetCurrentByEventIdAsync(int eventId, CancellationToken cancellationToken)
            => Task.FromResult(_attachments.FirstOrDefault(attachment => attachment.IsCurrent));
        public Task<Attachment?> GetCurrentByEventIdForUpdateAsync(int eventId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Attachment?> GetLatestByEventIdForUpdateAsync(int eventId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Attachment>> GetByEventIdAsync(int eventId, CancellationToken cancellationToken)
            => Task.FromResult(_attachments);
        public Task UpdateAsync(Attachment attachment, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingTelegramPublisher : ITelegramPublisher
    {
        public List<string> SentTexts { get; } = [];
        public List<(string TelegramFileId, string FileName)> SentDocuments { get; } = [];
        public List<(int MessageId, bool DisableNotification)> PinnedMessages { get; } = [];
        public List<int> UnpinnedMessageIds { get; } = [];
        public bool ThrowOnSendDocument { get; init; }

        public Task<int> SendMessageAsync(string text, CancellationToken cancellationToken)
        {
            SentTexts.Add(text);
            return Task.FromResult(1001);
        }

        public Task SendDocumentAsync(string telegramFileId, string fileName, CancellationToken cancellationToken)
        {
            if (ThrowOnSendDocument)
            {
                throw new InvalidOperationException("send document failed");
            }

            SentDocuments.Add((telegramFileId, fileName));
            return Task.CompletedTask;
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
