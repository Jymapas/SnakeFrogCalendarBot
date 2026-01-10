using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Application.UseCases.Events;

public sealed class ReplaceEventFile
{
    private readonly IEventRepository _eventRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IClock _clock;

    public ReplaceEventFile(
        IEventRepository eventRepository,
        IAttachmentRepository attachmentRepository,
        IClock clock)
    {
        _eventRepository = eventRepository;
        _attachmentRepository = attachmentRepository;
        _clock = clock;
    }

    public async Task ExecuteAsync(ReplaceEventFileCommand command, CancellationToken cancellationToken)
    {
        var events = await _eventRepository.ListUpcomingAsync(cancellationToken);
        var eventEntity = events.FirstOrDefault(e => e.Id == command.EventId);
        if (eventEntity is null)
        {
            throw new InvalidOperationException($"Event with id {command.EventId} not found.");
        }

        var existingAttachments = await _attachmentRepository.GetByEventIdAsync(command.EventId, cancellationToken);
        if (existingAttachments.Count == 0)
        {
            throw new InvalidOperationException($"No files to replace for event {command.EventId}.");
        }

        var latestAttachment = await _attachmentRepository.GetLatestByEventIdForUpdateAsync(command.EventId, cancellationToken);
        if (latestAttachment is null)
        {
            throw new InvalidOperationException($"Latest file not found for event {command.EventId}.");
        }

        latestAttachment.MarkAsNotCurrent();
        await _attachmentRepository.UpdateAsync(latestAttachment, cancellationToken);

        var nextVersion = existingAttachments.Max(a => a.Version) + 1;
        var newAttachment = new Attachment(
            command.EventId,
            command.TelegramFileId,
            command.TelegramFileUniqueId,
            command.FileName,
            command.MimeType,
            command.Size,
            nextVersion,
            true,
            _clock.UtcNow);

        await _attachmentRepository.AddAsync(newAttachment, cancellationToken);
    }
}

public sealed record ReplaceEventFileCommand(
    int EventId,
    string TelegramFileId,
    string TelegramFileUniqueId,
    string FileName,
    string? MimeType,
    long? Size);