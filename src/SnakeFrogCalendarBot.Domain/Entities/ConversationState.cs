using SnakeFrogCalendarBot.Domain.Exceptions;

namespace SnakeFrogCalendarBot.Domain.Entities;

public sealed class ConversationState
{
    public long UserId { get; private set; }
    public string ConversationName { get; private set; } = string.Empty;
    public string Step { get; private set; } = string.Empty;
    public string? StateJson { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private ConversationState()
    {
    }

    public ConversationState(long userId, string conversationName, string step, string? stateJson, DateTime createdAtUtc)
    {
        if (userId <= 0)
        {
            throw new DomainException("UserId must be positive.");
        }

        if (string.IsNullOrWhiteSpace(conversationName))
        {
            throw new DomainException("ConversationName is required.");
        }

        if (string.IsNullOrWhiteSpace(step))
        {
            throw new DomainException("Step is required.");
        }

        UserId = userId;
        ConversationName = conversationName.Trim();
        Step = step.Trim();
        StateJson = stateJson;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public void Update(string step, string? stateJson, DateTime updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            throw new DomainException("Step is required.");
        }

        Step = step.Trim();
        StateJson = stateJson;
        UpdatedAtUtc = updatedAtUtc;
    }
}
