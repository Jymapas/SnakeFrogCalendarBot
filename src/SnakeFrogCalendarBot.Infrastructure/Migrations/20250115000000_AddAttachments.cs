using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SnakeFrogCalendarBot.Infrastructure.Migrations;

public partial class AddAttachments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "attachments",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                event_id = table.Column<int>(type: "integer", nullable: false),
                telegram_file_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                telegram_file_unique_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                size = table.Column<long>(type: "bigint", nullable: true),
                version = table.Column<int>(type: "integer", nullable: false),
                is_current = table.Column<bool>(type: "boolean", nullable: false),
                uploaded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_attachments", x => x.id);
                table.ForeignKey(
                    name: "fk_attachments_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_attachments_event_id_is_current",
            table: "attachments",
            columns: new[] { "event_id", "is_current" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "attachments");
    }
}