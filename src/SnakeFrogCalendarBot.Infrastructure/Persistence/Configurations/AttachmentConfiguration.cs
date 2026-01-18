using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Infrastructure.Persistence.Configurations;

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("id");

        builder.Property(a => a.EventId)
            .IsRequired()
            .HasColumnName("event_id");

        builder.Property(a => a.TelegramFileId)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("telegram_file_id");

        builder.Property(a => a.TelegramFileUniqueId)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("telegram_file_unique_id");

        builder.Property(a => a.FileName)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("file_name");

        builder.Property(a => a.MimeType)
            .HasMaxLength(100)
            .HasColumnName("mime_type");

        builder.Property(a => a.Size)
            .HasColumnName("size");

        builder.Property(a => a.Version)
            .IsRequired()
            .HasColumnName("version");

        builder.Property(a => a.IsCurrent)
            .IsRequired()
            .HasColumnName("is_current");

        builder.Property(a => a.UploadedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasColumnName("uploaded_at_utc");

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(a => a.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.EventId, a.IsCurrent });
    }
}