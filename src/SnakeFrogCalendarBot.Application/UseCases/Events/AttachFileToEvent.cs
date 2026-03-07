using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace SnakeFrogCalendarBot.Application.UseCases.Events;

public sealed class AttachFileToEvent
{
    private readonly IEventRepository _eventRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IClock _clock;
    private readonly RefreshLatestDigestPosts _refreshLatestDigestPosts;
    private readonly ILogger<AttachFileToEvent> _logger;

    public AttachFileToEvent(
        IEventRepository eventRepository,
        IAttachmentRepository attachmentRepository,
        IClock clock,
        RefreshLatestDigestPosts refreshLatestDigestPosts,
        ILogger<AttachFileToEvent> logger)
    {
        _eventRepository = eventRepository;
        _attachmentRepository = attachmentRepository;
        _clock = clock;
        _refreshLatestDigestPosts = refreshLatestDigestPosts;
        _logger = logger;
    }

    public async Task ExecuteAsync(AttachFileToEventCommand command, CancellationToken cancellationToken)
    {
        var events = await _eventRepository.ListUpcomingAsync(cancellationToken);
        var eventEntity = events.FirstOrDefault(e => e.Id == command.EventId);
        if (eventEntity is null)
        {
            throw new InvalidOperationException($"Event with id {command.EventId} not found.");
        }

        var existingAttachments = await _attachmentRepository.GetByEventIdAsync(command.EventId, cancellationToken);
        var nextVersion = existingAttachments.Count > 0
            ? existingAttachments.Max(a => a.Version) + 1
            : 1;

        var attachment = new Attachment(
            command.EventId,
            command.TelegramFileId,
            command.TelegramFileUniqueId,
            command.FileName,
            command.MimeType,
            command.Size,
            nextVersion,
            true,
            _clock.UtcNow);

        await _attachmentRepository.AddAsync(attachment, cancellationToken);

        try
        {
            await _refreshLatestDigestPosts.ForEventAsync(eventEntity, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh latest digest posts after attaching file to event {EventId}", eventEntity.Id);
        }
    }
}

public sealed record AttachFileToEventCommand(
    int EventId,
    string TelegramFileId,
    string TelegramFileUniqueId,
    string FileName,
    string? MimeType,
    long? Size);
