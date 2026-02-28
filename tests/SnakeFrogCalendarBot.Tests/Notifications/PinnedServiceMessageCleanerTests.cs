using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using SnakeFrogCalendarBot.Application.Abstractions.Telegram;
using SnakeFrogCalendarBot.Worker.Config;
using SnakeFrogCalendarBot.Worker.Telegram;
using Telegram.Bot.Types;

namespace SnakeFrogCalendarBot.Tests.Notifications;

public sealed class PinnedServiceMessageCleanerTests
{
    [Test]
    public async Task TryDeleteAsync_ForRegisteredPinnedMessageInTargetChat_DeletesServiceMessage()
    {
        var telegramPublisher = new RecordingTelegramPublisher();
        var registry = new PinnedMessageCleanupRegistry();
        registry.RegisterPinnedMessage(500);

        var cleaner = new PinnedServiceMessageCleaner(
            telegramPublisher,
            registry,
            new AppOptions { TelegramTargetChat = "-100123" },
            NullLogger<PinnedServiceMessageCleaner>.Instance);

        var channelPost = CreateChannelPost(600, -100123, "mychannel", 500);

        await cleaner.TryDeleteAsync(channelPost, CancellationToken.None);

        Assert.That(telegramPublisher.DeletedMessageIds, Is.EquivalentTo(new[] { 600 }));
        Assert.That(registry.TryConsumePinnedMessage(500), Is.False);
    }

    [Test]
    public async Task TryDeleteAsync_ForNonTargetChat_DoesNothing()
    {
        var telegramPublisher = new RecordingTelegramPublisher();
        var registry = new PinnedMessageCleanupRegistry();
        registry.RegisterPinnedMessage(500);

        var cleaner = new PinnedServiceMessageCleaner(
            telegramPublisher,
            registry,
            new AppOptions { TelegramTargetChat = "-100999" },
            NullLogger<PinnedServiceMessageCleaner>.Instance);

        var channelPost = CreateChannelPost(600, -100123, "otherchannel", 500);

        await cleaner.TryDeleteAsync(channelPost, CancellationToken.None);

        Assert.That(telegramPublisher.DeletedMessageIds, Is.Empty);
        Assert.That(registry.TryConsumePinnedMessage(500), Is.True);
    }

    private static Message CreateChannelPost(int serviceMessageId, long chatId, string username, int pinnedMessageId)
    {
        var pinnedMessage = new Message
        {
            Chat = new Chat { Id = chatId, Username = username }
        };
        SetMessageId(pinnedMessage, pinnedMessageId);

        var channelPost = new Message
        {
            Chat = new Chat { Id = chatId, Username = username },
            PinnedMessage = pinnedMessage
        };
        SetMessageId(channelPost, serviceMessageId);
        return channelPost;
    }

    private static void SetMessageId(Message message, int messageId)
    {
        var property = typeof(Message).GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);
        if (property?.SetMethod is not null)
        {
            property.SetValue(message, messageId);
            return;
        }

        typeof(Message)
            .GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(message, messageId);
    }

    private sealed class RecordingTelegramPublisher : ITelegramPublisher
    {
        public List<int> DeletedMessageIds { get; } = [];

        public Task<int> SendMessageAsync(string text, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task EditMessageAsync(int messageId, string text, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteMessageAsync(int messageId, CancellationToken cancellationToken)
        {
            DeletedMessageIds.Add(messageId);
            return Task.CompletedTask;
        }

        public Task PinMessageAsync(int messageId, bool disableNotification, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UnpinMessageAsync(int messageId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
