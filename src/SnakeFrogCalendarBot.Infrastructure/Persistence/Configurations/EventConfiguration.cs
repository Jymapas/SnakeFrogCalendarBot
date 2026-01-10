using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Kind)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.IsAllDay)
            .IsRequired();

        builder.Property(e => e.OccursAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.Month);

        builder.Property(e => e.Day);

        builder.Property(e => e.TimeOfDay)
            .HasConversion(
                v => v.HasValue ? (long?)v.Value.Ticks : null,
                v => v.HasValue ? TimeSpan.FromTicks(v.Value) : null);

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.Place)
            .HasMaxLength(200);

        builder.Property(e => e.Link)
            .HasMaxLength(500);

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.UpdatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(e => new { e.Kind, e.Month, e.Day });
    }
}