using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Telegram;
using SnakeFrogCalendarBot.Application.Dto;
using SnakeFrogCalendarBot.Application.Formatting;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Tests.Notifications;

public sealed class RefreshLatestDigestPostsTests
{
    [Test]
    public async Task ForBirthdayAsync_OnPeriodEnd_UpdatesDailyWeeklyAndMonthlyPosts()
    {
        var birthday = new Birthday("Иван", 28, 2, 2000, "@ivan", new DateTime(2026, 2, 28, 9, 0, 0, DateTimeKind.Utc));
        var service = CreateService(
            birthdays: [birthday],
            latestPosts:
            [
                Latest(DigestType.Daily, 101, new DateTime(2026, 2, 28), new DateTime(2026, 2, 28, 23, 59, 59)),
                Latest(DigestType.Weekly, 102, new DateTime(2026, 2, 23), new DateTime(2026, 3, 1, 23, 59, 59)),
                Latest(DigestType.Monthly, 103, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28, 23, 59, 59))
            ]);

        await service.RefreshLatestDigestPosts.ForBirthdayAsync(birthday, CancellationToken.None);

        Assert.That(service.TelegramPublisher.EditedMessageIds, Is.EquivalentTo(new[] { 101, 102, 103 }));
        Assert.That(service.TelegramPublisher.EditedTexts.All(text => text.Contains("Иван", StringComparison.Ordinal)));
    }

    [Test]
    public async Task ForBirthdayAsync_OnPreviousDay_UpdatesWeeklyAndMonthlyPostsOnly()
    {
        var birthday = new Birthday("Петр", 27, 2, 2000, null, new DateTime(2026, 2, 28, 9, 0, 0, DateTimeKind.Utc));
        var service = CreateService(
            birthdays: [birthday],
            latestPosts:
            [
                Latest(DigestType.Daily, 101, new DateTime(2026, 2, 28), new DateTime(2026, 2, 28, 23, 59, 59)),
                Latest(DigestType.Weekly, 102, new DateTime(2026, 2, 23), new DateTime(2026, 3, 1, 23, 59, 59)),
                Latest(DigestType.Monthly, 103, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28, 23, 59, 59))
            ]);

        await service.RefreshLatestDigestPosts.ForBirthdayAsync(birthday, CancellationToken.None);

        Assert.That(service.TelegramPublisher.EditedMessageIds, Is.EquivalentTo(new[] { 102, 103 }));
        Assert.That(service.TelegramPublisher.EditedTexts.All(text => text.Contains("Петр", StringComparison.Ordinal)));
    }

    [Test]
    public async Task ForEventAsync_UpdatesOnlyMatchingWeeklyAndMonthlyPosts()
    {
        var @event = Event.CreateOneOff(
            "Совещание",
            new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.FromHours(3)),
            false,
            null,
            null,
            null,
            new DateTime(2026, 2, 28, 9, 0, 0, DateTimeKind.Utc));

        var service = CreateService(
            events: [@event],
            latestPosts:
            [
                Latest(DigestType.Daily, 201, new DateTime(2026, 2, 28), new DateTime(2026, 2, 28, 23, 59, 59)),
                Latest(DigestType.Weekly, 202, new DateTime(2026, 3, 2), new DateTime(2026, 3, 8, 23, 59, 59)),
                Latest(DigestType.Monthly, 203, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31, 23, 59, 59))
            ]);

        await service.RefreshLatestDigestPosts.ForEventAsync(@event, CancellationToken.None);

        Assert.That(service.TelegramPublisher.EditedMessageIds, Is.EquivalentTo(new[] { 202, 203 }));
        Assert.That(service.TelegramPublisher.EditedTexts.All(text => text.Contains("Совещание", StringComparison.Ordinal)));
    }

    private static TestFixture CreateService(
        IReadOnlyList<Birthday>? birthdays = null,
        IReadOnlyList<Event>? events = null,
        IReadOnlyList<LatestDigestPostInfo>? latestPosts = null)
    {
        var eventRepository = new FakeEventRepository(events ?? []);
        var birthdayRepository = new FakeBirthdayRepository(birthdays ?? []);
        var attachmentRepository = new FakeAttachmentRepository();
        var itemsProvider = new DigestItemsProvider(eventRepository, birthdayRepository, attachmentRepository);
        var formatter = new DigestFormatter();
        var telegramPublisher = new RecordingTelegramPublisher();
        var latestDigestPostRepository = new FakeLatestDigestPostRepository(latestPosts ?? []);
        var refreshLatestDigestPosts = new RefreshLatestDigestPosts(
            latestDigestPostRepository,
            itemsProvider,
            formatter,
            telegramPublisher);

        return new TestFixture(refreshLatestDigestPosts, telegramPublisher);
    }

    private static LatestDigestPostInfo Latest(
        DigestType digestType,
        int messageId,
        DateTime periodStartLocal,
        DateTime periodEndLocal,
        string timeZoneId = "Europe/Moscow")
    {
        return new LatestDigestPostInfo(digestType, messageId, periodStartLocal, periodEndLocal, timeZoneId);
    }

    private sealed record TestFixture(
        RefreshLatestDigestPosts RefreshLatestDigestPosts,
        RecordingTelegramPublisher TelegramPublisher);

    private sealed class FakeLatestDigestPostRepository : ILatestDigestPostRepository
    {
        private readonly IReadOnlyList<LatestDigestPostInfo> _latestPosts;

        public FakeLatestDigestPostRepository(IReadOnlyList<LatestDigestPostInfo> latestPosts)
        {
            _latestPosts = latestPosts;
        }

        public Task UpsertAsync(
            DigestType digestType,
            int notificationRunId,
            int telegramMessageId,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<LatestDigestPostInfo>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_latestPosts);
        }
    }

    private sealed class FakeBirthdayRepository : IBirthdayRepository
    {
        private readonly IReadOnlyList<Birthday> _birthdays;

        public FakeBirthdayRepository(IReadOnlyList<Birthday> birthdays)
        {
            _birthdays = birthdays;
        }

        public Task AddAsync(Birthday birthday, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<Birthday>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(_birthdays);

        public Task<Birthday?> GetByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task UpdateAsync(Birthday birthday, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
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

    private sealed class FakeAttachmentRepository : IAttachmentRepository
    {
        public Task AddAsync(Attachment attachment, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Attachment?> GetCurrentByEventIdAsync(int eventId, CancellationToken cancellationToken)
        {
            return Task.FromResult<Attachment?>(null);
        }

        public Task<Attachment?> GetCurrentByEventIdForUpdateAsync(int eventId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Attachment?> GetLatestByEventIdForUpdateAsync(int eventId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<Attachment>> GetByEventIdAsync(int eventId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task UpdateAsync(Attachment attachment, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingTelegramPublisher : ITelegramPublisher
    {
        public List<int> EditedMessageIds { get; } = [];
        public List<string> EditedTexts { get; } = [];

        public Task<int> SendMessageAsync(string text, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task EditMessageAsync(int messageId, string text, CancellationToken cancellationToken)
        {
            EditedMessageIds.Add(messageId);
            EditedTexts.Add(text);
            return Task.CompletedTask;
        }
    }
}
