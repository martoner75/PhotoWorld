using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <summary>
    /// Keeps <c>photoworld.app_logs</c> to the last 30 days with a nightly pg_cron job (03:15 UTC).
    /// pg_cron ships with Supabase; this enables it if it isn't already.
    /// </summary>
    public partial class LogRetention : Migration
    {
        private const string JobName = "photoworld-app-logs-retention";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("create extension if not exists pg_cron with schema pg_catalog;");

            // cron.schedule with a job name replaces any existing job of that name, so this is re-runnable.
            migrationBuilder.Sql($"""
                select cron.schedule(
                    '{JobName}',
                    '15 3 * * *',
                    $$delete from photoworld.app_logs where timestamp < now() - interval '30 days'$$
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"select cron.unschedule('{JobName}') where exists (select 1 from cron.job where jobname = '{JobName}');");
        }
    }
}
