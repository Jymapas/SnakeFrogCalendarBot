using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SnakeFrogCalendarBot.Infrastructure.Migrations;

public partial class AddEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "events",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                kind = table.Column<int>(type: "integer", nullable: false),
                is_all_day = table.Column<bool>(type: "boolean", nullable: false),
                occurs_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                month = table.Column<int>(type: "integer", nullable: true),
                day = table.Column<int>(type: "integer", nullable: true),
                time_of_day = table.Column<long>(type: "bigint", nullable: true),
                description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                place = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_events", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_events_kind_month_day",
            table: "events",
            columns: new[] { "kind", "month", "day" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "events");
    }
}