using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Application.UseCases.Events;

public sealed class AttachFileToEvent
{
    private readonly IEventRepository _eventRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IClock _clock;

    public AttachFileToEvent(
        IEventRepository eventRepository,
        IAttachmentRepository attachmentRepository,
        IClock clock)
    {
        _eventRepository = eventRepository;
        _attachmentRepository = attachmentRepository;
        _clock = clock;
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
    }
}

public sealed record AttachFileToEventCommand(
    int EventId,
    string TelegramFileId,
    string TelegramFileUniqueId,
    string FileName,
    string? MimeType,
    long? Size);