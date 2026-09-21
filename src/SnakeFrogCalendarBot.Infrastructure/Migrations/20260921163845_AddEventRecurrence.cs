using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SnakeFrogCalendarBot.Infrastructure.Migrations;

public partial class AddEventRecurrence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Идемпотентно: на «старых» базах схема events могла быть создана fallback-SQL из Worker,
        // минуя историю миграций, и часть колонок уже существует.
        migrationBuilder.Sql(
            """
            ALTER TABLE events ADD COLUMN IF NOT EXISTS days_of_week_mask INTEGER;
            ALTER TABLE events ADD COLUMN IF NOT EXISTS interval_count INTEGER;
            ALTER TABLE events ADD COLUMN IF NOT EXISTS interval_unit INTEGER;
            ALTER TABLE events ADD COLUMN IF NOT EXISTS start_date_local DATE;
            ALTER TABLE events ADD COLUMN IF NOT EXISTS repeat_until_local DATE;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE events DROP COLUMN IF EXISTS days_of_week_mask;
            ALTER TABLE events DROP COLUMN IF EXISTS interval_count;
            ALTER TABLE events DROP COLUMN IF EXISTS interval_unit;
            ALTER TABLE events DROP COLUMN IF EXISTS start_date_local;
            ALTER TABLE events DROP COLUMN IF EXISTS repeat_until_local;
            """);
    }
}
