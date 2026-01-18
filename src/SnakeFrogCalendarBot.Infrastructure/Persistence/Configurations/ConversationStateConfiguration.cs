using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Infrastructure.Persistence.Configurations;

public sealed class ConversationStateConfiguration : IEntityTypeConfiguration<ConversationState>
{
    public void Configure(EntityTypeBuilder<ConversationState> builder)
    {
        builder.ToTable("conversation_states");

        builder.HasKey(state => state.UserId);
        builder.Property(state => state.UserId)
            .HasColumnName("user_id");

        builder.Property(state => state.ConversationName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("conversation_name");

        builder.Property(state => state.Step)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("step");

        builder.Property(state => state.StateJson)
            .HasColumnType("text")
            .HasColumnName("state_json");

        builder.Property(state => state.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasColumnName("created_at_utc");

        builder.Property(state => state.UpdatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasColumnName("updated_at_utc");
    }
}
