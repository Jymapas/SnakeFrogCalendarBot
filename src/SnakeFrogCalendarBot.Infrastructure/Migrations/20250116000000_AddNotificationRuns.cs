using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SnakeFrogCalendarBot.Infrastructure.Migrations;

public partial class AddNotificationRuns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "notification_runs",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                digest_type = table.Column<int>(type: "integer", nullable: false),
                period_start_local = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                period_end_local = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_notification_runs", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_notification_runs_digest_type_period_start_local_period_end_lo",
            table: "notification_runs",
            columns: new[] { "digest_type", "period_start_local", "period_end_local", "time_zone_id" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "notification_runs");
    }
}