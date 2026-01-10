using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Application.UseCases.Events;

public sealed class GetEventWithAttachment
{
    private readonly IEventRepository _eventRepository;
    private readonly IAttachmentRepository _attachmentRepository;

    public GetEventWithAttachment(
        IEventRepository eventRepository,
        IAttachmentRepository attachmentRepository)
    {
        _eventRepository = eventRepository;
        _attachmentRepository = attachmentRepository;
    }

    public async Task<EventWithAttachmentResult?> ExecuteAsync(int eventId, CancellationToken cancellationToken)
    {
        var events = await _eventRepository.ListUpcomingAsync(cancellationToken);
        var eventEntity = events.FirstOrDefault(e => e.Id == eventId);
        if (eventEntity is null)
        {
            return null;
        }

        var attachment = await _attachmentRepository.GetCurrentByEventIdAsync(eventId, cancellationToken);

        return new EventWithAttachmentResult(eventEntity, attachment);
    }
}

public sealed record EventWithAttachmentResult(Event Event, Attachment? Attachment);