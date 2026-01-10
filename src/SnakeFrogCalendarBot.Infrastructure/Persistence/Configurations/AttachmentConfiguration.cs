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

        builder.Property(a => a.EventId)
            .IsRequired();

        builder.Property(a => a.TelegramFileId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.TelegramFileUniqueId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.FileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.MimeType)
            .HasMaxLength(100);

        builder.Property(a => a.Size);

        builder.Property(a => a.Version)
            .IsRequired();

        builder.Property(a => a.IsCurrent)
            .IsRequired();

        builder.Property(a => a.UploadedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(a => a.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.EventId, a.IsCurrent });
    }
}