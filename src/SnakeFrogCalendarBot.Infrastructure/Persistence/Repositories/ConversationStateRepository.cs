using Microsoft.EntityFrameworkCore;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Infrastructure.Persistence.Repositories;

public sealed class ConversationStateRepository : IConversationStateRepository
{
    private readonly CalendarDbContext _dbContext;

    public ConversationStateRepository(CalendarDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ConversationState?> GetByUserIdAsync(long userId, CancellationToken cancellationToken)
    {
        return _dbContext.ConversationStates
            .AsNoTracking()
            .FirstOrDefaultAsync(state => state.UserId == userId, cancellationToken);
    }

    public async Task UpsertAsync(ConversationState state, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.ConversationStates
            .FirstOrDefaultAsync(current => current.UserId == state.UserId, cancellationToken);

        if (existing is null)
        {
            _dbContext.ConversationStates.Add(state);
        }
        else
        {
            _dbContext.Entry(existing).CurrentValues.SetValues(state);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(long userId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.ConversationStates
            .FirstOrDefaultAsync(current => current.UserId == userId, cancellationToken);

        if (existing is null)
        {
            return;
        }

        _dbContext.ConversationStates.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
