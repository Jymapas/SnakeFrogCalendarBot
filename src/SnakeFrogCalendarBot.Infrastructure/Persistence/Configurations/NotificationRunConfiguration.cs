using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Infrastructure.Persistence.Configurations;

public sealed class NotificationRunConfiguration : IEntityTypeConfiguration<NotificationRun>
{
    public void Configure(EntityTypeBuilder<NotificationRun> builder)
    {
        builder.ToTable("notification_runs");

        builder.HasKey(nr => nr.Id);
        builder.Property(nr => nr.Id)
            .HasColumnName("id");

        builder.Property(nr => nr.DigestType)
            .IsRequired()
            .HasConversion<int>()
            .HasColumnName("digest_type");

        builder.Property(nr => nr.PeriodStartLocal)
            .IsRequired()
            .HasColumnType("timestamp without time zone")
            .HasColumnName("period_start_local");

        builder.Property(nr => nr.PeriodEndLocal)
            .IsRequired()
            .HasColumnType("timestamp without time zone")
            .HasColumnName("period_end_local");

        builder.Property(nr => nr.TimeZoneId)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("time_zone_id");

        builder.Property(nr => nr.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasColumnName("created_at_utc");

        builder.HasIndex(nr => new { nr.DigestType, nr.PeriodStartLocal, nr.PeriodEndLocal, nr.TimeZoneId })
            .IsUnique();
    }
}