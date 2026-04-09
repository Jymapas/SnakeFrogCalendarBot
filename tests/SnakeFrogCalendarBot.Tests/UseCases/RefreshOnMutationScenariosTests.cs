using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Telegram;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Dto;
using SnakeFrogCalendarBot.Application.Formatting;
using SnakeFrogCalendarBot.Application.UseCases.Birthdays;
using SnakeFrogCalendarBot.Application.UseCases.Events;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Tests.UseCases;

public sealed class RefreshOnMutationScenariosTests
{
    [Test]
    public async Task CreateEvent_RefreshesLatestPosts()
    {
        var eventRepository = new FakeEventRepository([]);
        var birthdayRepository = new FakeBirthdayRepository([]);
        var attachmentRepository = new FakeAttachmentRepository([]);
        var telegramPublisher = new RecordingTelegramPublisher();
        var refresh = CreateRefreshService(eventRepository, birthdayRepository, attachmentRepository, telegramPublisher, BuildMarchEventPosts());
        var useCase = new CreateEvent(
            eventRepository,
            new TestClock(),
            new TestTimeZoneProvider(),
            refresh,
            NullLogger<CreateEvent>.Instance);

        await useCase.ExecuteAsync(
            new CreateEventCommand(
                "Новая встреча",
                EventKind.OneOff,
                false,
                new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.FromHours(3)),
                null,
                null,
                null,
                null,
                null,
                null),
            CancellationToken.None);

        Assert.That(telegramPublisher.EditedMessageIds, Is.EquivalentTo(new[] { 101, 102, 103 }));
    }

    [Test]
    public async Task CreateBirthday_RefreshesLatestPosts()
    {
        var eventRepository = new FakeEventRepository([]);
        var birthdayRepository = new FakeBirthdayRepository([]);
        var attachmentRepository = new FakeAttachmentRepository([]);
        var telegramPublisher = new RecordingTelegramPublisher();
        var refresh = CreateRefreshService(eventRepository, birthdayRepository, attachmentRepository, telegramPublisher, BuildFebruaryBirthdayPosts());
        var useCase = new CreateBirthday(
            birthdayRepository,
            new TestClock(),
            refresh,
            NullLogger<CreateBirthday>.Instance);

        await useCase.ExecuteAsync(
            new CreateBirthdayCommand("Иван", 28, 2, 2000, "@ivan"),
            CancellationToken.None);

        Assert.That(telegramPublisher.EditedMessageIds, Is.EquivalentTo(new[] { 201, 202, 203 }));
    }

    [Test]
    public async Task AttachFileToEvent_RefreshesLatestPostsAndShowsAttachmentIndicator()
    {
        var eventEntity = CreateEventWithId(1, "Встреча", new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.FromHours(3)));
        var eventRepository = new FakeEventRepository([eventEntity]);
        var birthdayRepository = new FakeBirthdayRepository([]);
        var attachmentRepository = new FakeAttachmentRepository([]);
        var telegramPublisher = new RecordingTelegramPublisher();
        var refresh = CreateRefreshService(eventRepository, birthdayRepository, attachmentRepository, telegramPublisher, BuildMarchEventPosts());
        var useCase = new AttachFileToEvent(
            eventRepository,
            attachmentRepository,
            new TestClock(),
            refresh,
            NullLogger<AttachFileToEvent>.Instance);

        await useCase.ExecuteAsync(
            new AttachFileToEventCommand(1, "file-id-1", "uniq-1", "agenda.pdf", "application/pdf", 1234),
            CancellationToken.None);

        Assert.That(telegramPublisher.EditedMessageIds, Is.EquivalentTo(new[] { 101, 102, 103 }));
        Assert.That(telegramPublisher.EditedTexts.All(text => text.Contains("📎", StringComparison.Ordinal)));
    }

    [Test]
    public async Task ReplaceEventFile_RefreshesLatestPostsAndShowsAttachmentIndicator()
    {
        var eventEntity = CreateEventWithId(1, "Встреча", new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.FromHours(3)));
        var eventRepository = new FakeEventRepository([eventEntity]);
        var birthdayRepository = new FakeBirthdayRepository([]);
        var initialAttachment = new Attachment(1, "old-file-id", "old-uniq-id", "old.pdf", "application/pdf", 100, 1, true, DateTime.UtcNow);
        var attachmentRepository = new FakeAttachmentRepository([initialAttachment]);
        var telegramPublisher = new RecordingTelegramPublisher();
        var refresh = CreateRefreshService(eventRepository, birthdayRepository, attachmentRepository, telegramPublisher, BuildMarchEventPosts());
        var useCase = new ReplaceEventFile(
            eventRepository,
            attachmentRepository,
            new TestClock(),
            refresh,
            NullLogger<ReplaceEventFile>.Instance);

        await useCase.ExecuteAsync(
            new ReplaceEventFileCommand(1, "new-file-id", "new-uniq-id", "new.pdf", "application/pdf", 200),
            CancellationToken.None);

        Assert.That(telegramPublisher.EditedMessageIds, Is.EquivalentTo(new[] { 101, 102, 103 }));
        Assert.That(telegramPublisher.EditedTexts.All(text => text.Contains("📎", StringComparison.Ordinal)));
    }

    [Test]
    public async Task UpdateEvent_RefreshesLatestPosts()
    {
        var eventEntity = CreateEventWithId(1, "Старое название", new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.FromHours(3)));
        var eventRepository = new FakeEventRepository([eventEntity]);
        var birthdayRepository = new FakeBirthdayRepository([]);
        var attachmentRepository = new FakeAttachmentRepository([]);
        var telegramPublisher = new RecordingTelegramPublisher();
        var refresh = CreateRefreshService(eventRepository, birthdayRepository, attachmentRepository, telegramPublisher, BuildMarchEventPosts());
        var useCase = new UpdateEvent(
            eventRepository,
            new TestClock(),
            refresh,
            NullLogger<UpdateEvent>.Instance);

        await useCase.ExecuteAsync(
            new UpdateEventCommand(
                1,
                "title",
                "Новое название",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null),
            CancellationToken.None);

        Assert.That(telegramPublisher.EditedMessageIds, Is.EquivalentTo(new[] { 101, 102, 103 }));
        Assert.That(telegramPublisher.EditedTexts.All(text => text.Contains("Новое название", StringComparison.Ordinal)));
    }

    [Test]
    public async Task DeleteEvent_RefreshesLatestPosts()
    {
        var eventEntity = CreateEventWithId(1, "Встреча", new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.FromHours(3)));
        var eventRepository = new FakeEventRepository([eventEntity]);
        var birthdayRepository = new FakeBirthdayRepository([]);
        var attachmentRepository = new FakeAttachmentRepository([]);
        var telegramPublisher = new RecordingTelegramPublisher();
        var refresh = CreateRefreshService(eventRepository, birthdayRepository, attachmentRepository, telegramPublisher, BuildMarchEventPosts());
        var useCase = new DeleteEvent(
            eventRepository,
            refresh,
            NullLogger<DeleteEvent>.Instance);

        await useCase.ExecuteAsync(1, CancellationToken.None);

        Assert.That(telegramPublisher.EditedMessageIds, Is.EquivalentTo(new[] { 101, 102, 103 }));
    }

    [Test]
    public async Task UpdateBirthday_RefreshesLatestPosts()
    {
        var birthday = CreateBirthdayWithId(10, "Иван", 28, 2);
        var eventRepository = new FakeEventRepository([]);
        var birthdayRepository = new FakeBirthdayRepository([birthday]);
        var attachmentRepository = new FakeAttachmentRepository([]);
        var telegramPublisher = new RecordingTelegramPublisher();
        var refresh = CreateRefreshService(eventRepository, birthdayRepository, attachmentRepository, telegramPublisher, BuildFebruaryBirthdayPosts());
        var useCase = new UpdateBirthday(
            birthdayRepository,
            new TestClock(),
            refresh,
            NullLogger<UpdateBirthday>.Instance);

        await useCase.ExecuteAsync(
            new UpdateBirthdayCommand(10, "contact", null, null, null, null, "@ivan"),
            CancellationToken.None);

        Assert.That(telegramPublisher.EditedMessageIds, Is.EquivalentTo(new[] { 201, 202, 203 }));
        Assert.That(telegramPublisher.EditedTexts.All(text => text.Contains("Иван", StringComparison.Ordinal)));
    }

    [Test]
    public async Task DeleteBirthday_RefreshesLatestPosts()
    {
        var birthday = CreateBirthdayWithId(10, "Иван", 28, 2);
        var eventRepository = new FakeEventRepository([]);
        var birthdayRepository = new FakeBirthdayRepository([birthday]);
        var attachmentRepository = new FakeAttachmentRepository([]);
        var telegramPublisher = new RecordingTelegramPublisher();
        var refresh = CreateRefreshService(eventRepository, birthdayRepository, attachmentRepository, telegramPublisher, BuildFebruaryBirthdayPosts());
        var useCase = new DeleteBirthday(
            birthdayRepository,
            refresh,
            NullLogger<DeleteBirthday>.Instance);

        await useCase.ExecuteAsync(10, CancellationToken.None);

        Assert.That(telegramPublisher.EditedMessageIds, Is.EquivalentTo(new[] { 201, 202, 203 }));
    }

    private static RefreshLatestDigestPosts CreateRefreshService(
        IEventRepository eventRepository,
        IBirthdayRepository birthdayRepository,
        IAttachmentRepository attachmentRepository,
        RecordingTelegramPublisher telegramPublisher,
        IReadOnlyList<LatestDigestPostInfo> latestPosts)
    {
        var itemsProvider = new DigestItemsProvider(eventRepository, birthdayRepository, attachmentRepository);
        var formatter = new DigestFormatter();
        var latestDigestPostRepository = new FakeLatestDigestPostRepository(latestPosts);
        return new RefreshLatestDigestPosts(latestDigestPostRepository, itemsProvider, formatter, telegramPublisher);
    }

    private static IReadOnlyList<LatestDigestPostInfo> BuildMarchEventPosts()
    {
        return
        [
            new LatestDigestPostInfo(DigestType.Daily, 101, new DateTime(2026, 3, 2), new DateTime(2026, 3, 2, 23, 59, 59), "Europe/Moscow"),
            new LatestDigestPostInfo(DigestType.Weekly, 102, new DateTime(2026, 3, 2), new DateTime(2026, 3, 8, 23, 59, 59), "Europe/Moscow"),
            new LatestDigestPostInfo(DigestType.Monthly, 103, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31, 23, 59, 59), "Europe/Moscow")
        ];
    }

    private static IReadOnlyList<LatestDigestPostInfo> BuildFebruaryBirthdayPosts()
    {
        return
        [
            new LatestDigestPostInfo(DigestType.Daily, 201, new DateTime(2026, 2, 28), new DateTime(2026, 2, 28, 23, 59, 59), "Europe/Moscow"),
            new LatestDigestPostInfo(DigestType.Weekly, 202, new DateTime(2026, 2, 23), new DateTime(2026, 3, 1, 23, 59, 59), "Europe/Moscow"),
            new LatestDigestPostInfo(DigestType.Monthly, 203, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28, 23, 59, 59), "Europe/Moscow")
        ];
    }

    private static Event CreateEventWithId(int id, string title, DateTimeOffset occursAtUtc)
    {
        var eventEntity = Event.CreateOneOff(title, occursAtUtc, false, null, null, null, DateTime.UtcNow);
        SetEntityId(eventEntity, id);
        return eventEntity;
    }

    private static Birthday CreateBirthdayWithId(int id, string personName, int day, int month)
    {
        var birthday = new Birthday(personName, day, month, null, null, DateTime.UtcNow);
        SetEntityId(birthday, id);
        return birthday;
    }

    private static void SetEntityId(object entity, int id)
    {
        var property = entity.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);
        if (property?.SetMethod is not null)
        {
            property.SetValue(entity, id);
            return;
        }

        entity.GetType()
            .GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(entity, id);
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow => new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
    }

    private sealed class TestTimeZoneProvider : ITimeZoneProvider
    {
        public string GetTimeZoneId() => "Europe/Moscow";
    }

    private sealed class FakeEventRepository : IEventRepository
    {
        private readonly List<Event> _events;

        public FakeEventRepository(IReadOnlyList<Event> events)
        {
            _events = events.ToList();
        }

        public Task AddAsync(Event eventEntity, CancellationToken cancellationToken)
        {
            _events.Add(eventEntity);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Event>> ListUpcomingAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Event>>(_events);

        public Task<IReadOnlyList<Event>> ListAllAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Event>>(_events);

        public Task<IReadOnlyList<Event>> ListUpcomingForEditAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Event>>(_events);

        public Task<Event?> GetByIdAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult(_events.FirstOrDefault(@event => @event.Id == id));

        public Task UpdateAsync(Event eventEntity, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(int id, CancellationToken cancellationToken)
        {
            _events.RemoveAll(@event => @event.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBirthdayRepository : IBirthdayRepository
    {
        private readonly List<Birthday> _birthdays;

        public FakeBirthdayRepository(IReadOnlyList<Birthday> birthdays)
        {
            _birthdays = birthdays.ToList();
        }

        public Task AddAsync(Birthday birthday, CancellationToken cancellationToken)
        {
            _birthdays.Add(birthday);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Birthday>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Birthday>>(_birthdays);

        public Task<Birthday?> GetByIdAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult(_birthdays.FirstOrDefault(birthday => birthday.Id == id));

        public Task UpdateAsync(Birthday birthday, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(int id, CancellationToken cancellationToken)
        {
            _birthdays.RemoveAll(birthday => birthday.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAttachmentRepository : IAttachmentRepository
    {
        private readonly List<Attachment> _attachments;

        public FakeAttachmentRepository(IReadOnlyList<Attachment> attachments)
        {
            _attachments = attachments.ToList();
        }

        public Task AddAsync(Attachment attachment, CancellationToken cancellationToken)
        {
            _attachments.Add(attachment);
            return Task.CompletedTask;
        }

        public Task<Attachment?> GetCurrentByEventIdAsync(int eventId, CancellationToken cancellationToken)
            => Task.FromResult(_attachments.FirstOrDefault(attachment => attachment.EventId == eventId && attachment.IsCurrent));

        public Task<Attachment?> GetCurrentByEventIdForUpdateAsync(int eventId, CancellationToken cancellationToken)
            => Task.FromResult(_attachments.FirstOrDefault(attachment => attachment.EventId == eventId && attachment.IsCurrent));

        public Task<Attachment?> GetLatestByEventIdForUpdateAsync(int eventId, CancellationToken cancellationToken)
            => Task.FromResult(_attachments
                .Where(attachment => attachment.EventId == eventId)
                .OrderByDescending(attachment => attachment.Version)
                .FirstOrDefault());

        public Task<IReadOnlyList<Attachment>> GetByEventIdAsync(int eventId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Attachment>>(
                _attachments
                    .Where(attachment => attachment.EventId == eventId)
                    .OrderByDescending(attachment => attachment.Version)
                    .ToList());

        public Task UpdateAsync(Attachment attachment, CancellationToken cancellationToken) => Task.CompletedTask;
    }

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
            => throw new NotSupportedException();

        public Task<LatestDigestPostInfo?> GetByDigestTypeAsync(DigestType digestType, CancellationToken cancellationToken)
            => Task.FromResult(_latestPosts.FirstOrDefault(post => post.DigestType == digestType));

        public Task<IReadOnlyList<LatestDigestPostInfo>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult(_latestPosts);
    }

    private sealed class RecordingTelegramPublisher : ITelegramPublisher
    {
        public List<int> EditedMessageIds { get; } = [];
        public List<string> EditedTexts { get; } = [];

        public Task<int> SendMessageAsync(string text, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SendDocumentAsync(string telegramFileId, string fileName, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task EditMessageAsync(int messageId, string text, CancellationToken cancellationToken)
        {
            EditedMessageIds.Add(messageId);
            EditedTexts.Add(text);
            return Task.CompletedTask;
        }

        public Task DeleteMessageAsync(int messageId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task PinMessageAsync(int messageId, bool disableNotification, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UnpinMessageAsync(int messageId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
