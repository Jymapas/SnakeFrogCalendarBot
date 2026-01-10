using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SnakeFrogCalendarBot.Infrastructure.Persistence;

#nullable disable

namespace SnakeFrogCalendarBot.Infrastructure.Migrations;

[DbContext(typeof(CalendarDbContext))]
partial class CalendarDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "9.0.4")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("SnakeFrogCalendarBot.Domain.Entities.Birthday", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("integer");

            Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

            b.Property<int>("BirthYear")
                .HasColumnType("integer");

            b.Property<string>("Contact")
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            b.Property<DateTime>("CreatedAtUtc")
                .HasColumnType("timestamp with time zone");

            b.Property<int>("Day")
                .HasColumnType("integer");

            b.Property<int>("Month")
                .HasColumnType("integer");

            b.Property<string>("PersonName")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            b.Property<DateTime>("UpdatedAtUtc")
                .HasColumnType("timestamp with time zone");

            b.HasKey("Id");

            b.HasIndex("Month", "Day");

            b.ToTable("birthdays");
        });

        modelBuilder.Entity("SnakeFrogCalendarBot.Domain.Entities.ConversationState", b =>
        {
            b.Property<string>("ConversationName")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            b.Property<DateTime>("CreatedAtUtc")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("StateJson")
                .HasColumnType("text");

            b.Property<string>("Step")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            b.Property<DateTime>("UpdatedAtUtc")
                .HasColumnType("timestamp with time zone");

            b.Property<long>("UserId")
                .HasColumnType("bigint");

            b.HasKey("UserId");

            b.ToTable("conversation_states");
        });
    }
}
