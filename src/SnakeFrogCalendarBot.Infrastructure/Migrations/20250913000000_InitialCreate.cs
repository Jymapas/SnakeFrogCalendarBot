using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SnakeFrogCalendarBot.Infrastructure.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "birthdays",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                person_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                day = table.Column<int>(type: "integer", nullable: false),
                month = table.Column<int>(type: "integer", nullable: false),
                birth_year = table.Column<int>(type: "integer", nullable: true),
                contact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_birthdays", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "conversation_states",
            columns: table => new
            {
                user_id = table.Column<long>(type: "bigint", nullable: false),
                conversation_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                step = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                state_json = table.Column<string>(type: "text", nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_conversation_states", x => x.user_id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_birthdays_month_day",
            table: "birthdays",
            columns: new[] { "month", "day" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "conversation_states");

        migrationBuilder.DropTable(
            name: "birthdays");
    }
}
