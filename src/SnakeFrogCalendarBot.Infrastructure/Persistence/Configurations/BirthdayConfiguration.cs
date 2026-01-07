using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Infrastructure.Persistence.Configurations;

public sealed class BirthdayConfiguration : IEntityTypeConfiguration<Birthday>
{
    public void Configure(EntityTypeBuilder<Birthday> builder)
    {
        builder.ToTable("birthdays");

        builder.HasKey(birthday => birthday.Id);

        builder.Property(birthday => birthday.PersonName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(birthday => birthday.Day)
            .IsRequired();

        builder.Property(birthday => birthday.Month)
            .IsRequired();

        builder.Property(birthday => birthday.BirthYear);

        builder.Property(birthday => birthday.Contact)
            .HasMaxLength(200);

        builder.Property(birthday => birthday.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(birthday => birthday.UpdatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(birthday => new { birthday.Month, birthday.Day });
    }
}
