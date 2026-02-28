using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Infrastructure.Persistence.Configurations;

public sealed class LatestDigestPostConfiguration : IEntityTypeConfiguration<LatestDigestPost>
{
    public void Configure(EntityTypeBuilder<LatestDigestPost> builder)
    {
        builder.ToTable("latest_digest_posts");

        builder.HasKey(post => post.Id);
        builder.Property(post => post.Id)
            .HasColumnName("id");

        builder.Property(post => post.DigestType)
            .IsRequired()
            .HasConversion<int>()
            .HasColumnName("digest_type");

        builder.Property(post => post.NotificationRunId)
            .IsRequired()
            .HasColumnName("notification_run_id");

        builder.Property(post => post.TelegramMessageId)
            .IsRequired()
            .HasColumnName("telegram_message_id");

        builder.Property(post => post.UpdatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasColumnName("updated_at_utc");

        builder.HasIndex(post => post.DigestType)
            .IsUnique()
            .HasDatabaseName("ix_latest_digest_posts_digest_type");

        builder.HasIndex(post => post.NotificationRunId)
            .IsUnique()
            .HasDatabaseName("ix_latest_digest_posts_notification_run_id");
    }
}
