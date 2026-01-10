using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Application.Abstractions.Persistence;

public interface IConversationStateRepository
{
    Task<ConversationState?> GetByUserIdAsync(long userId, CancellationToken cancellationToken);
    Task UpsertAsync(ConversationState state, CancellationToken cancellationToken);
    Task DeleteAsync(long userId, CancellationToken cancellationToken);
}
