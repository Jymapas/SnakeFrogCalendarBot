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
        builder.Property(birthday => birthday.Id)
            .HasColumnName("id");

        builder.Property(birthday => birthday.PersonName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("person_name");

        builder.Property(birthday => birthday.Day)
            .IsRequired()
            .HasColumnName("day");

        builder.Property(birthday => birthday.Month)
            .IsRequired()
            .HasColumnName("month");

        builder.Property(birthday => birthday.BirthYear)
            .HasColumnName("birth_year");

        builder.Property(birthday => birthday.Contact)
            .HasMaxLength(200)
            .HasColumnName("contact");

        builder.Property(birthday => birthday.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasColumnName("created_at_utc");

        builder.Property(birthday => birthday.UpdatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasColumnName("updated_at_utc");

        builder.HasIndex(birthday => new { birthday.Month, birthday.Day });
    }
}
