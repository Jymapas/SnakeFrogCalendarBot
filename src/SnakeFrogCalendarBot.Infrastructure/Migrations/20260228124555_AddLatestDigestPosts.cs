using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SnakeFrogCalendarBot.Infrastructure.Migrations;

public partial class AddLatestDigestPosts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS latest_digest_posts (
                id SERIAL PRIMARY KEY,
                digest_type INTEGER NOT NULL,
                notification_run_id INTEGER NOT NULL,
                telegram_message_id INTEGER NOT NULL,
                updated_at_utc TIMESTAMP WITH TIME ZONE NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_latest_digest_posts_digest_type
            ON latest_digest_posts (digest_type);

            CREATE UNIQUE INDEX IF NOT EXISTS ix_latest_digest_posts_notification_run_id
            ON latest_digest_posts (notification_run_id);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "latest_digest_posts");
    }
}
