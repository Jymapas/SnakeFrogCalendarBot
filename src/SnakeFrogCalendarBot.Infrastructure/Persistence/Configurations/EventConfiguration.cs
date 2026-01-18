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
        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("title");

        builder.Property(e => e.Kind)
            .IsRequired()
            .HasConversion<int>()
            .HasColumnName("kind");

        builder.Property(e => e.IsAllDay)
            .IsRequired()
            .HasColumnName("is_all_day");

        builder.Property(e => e.OccursAtUtc)
            .HasColumnType("timestamp with time zone")
            .HasColumnName("occurs_at_utc");

        builder.Property(e => e.Month)
            .HasColumnName("month");

        builder.Property(e => e.Day)
            .HasColumnName("day");

        builder.Property(e => e.TimeOfDay)
            .HasConversion(
                v => v.HasValue ? (long?)v.Value.Ticks : null,
                v => v.HasValue ? TimeSpan.FromTicks(v.Value) : null)
            .HasColumnName("time_of_day");

        builder.Property(e => e.Description)
            .HasMaxLength(1000)
            .HasColumnName("description");

        builder.Property(e => e.Place)
            .HasMaxLength(200)
            .HasColumnName("place");

        builder.Property(e => e.Link)
            .HasMaxLength(500)
            .HasColumnName("link");

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasColumnName("created_at_utc");

        builder.Property(e => e.UpdatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasColumnName("updated_at_utc");

        builder.HasIndex(e => new { e.Kind, e.Month, e.Day });
    }
}