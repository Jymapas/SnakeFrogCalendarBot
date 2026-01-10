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

        builder.Property(nr => nr.DigestType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(nr => nr.PeriodStartLocal)
            .IsRequired();

        builder.Property(nr => nr.PeriodEndLocal)
            .IsRequired();

        builder.Property(nr => nr.TimeZoneId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(nr => nr.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(nr => new { nr.DigestType, nr.PeriodStartLocal, nr.PeriodEndLocal, nr.TimeZoneId })
            .IsUnique();
    }
}