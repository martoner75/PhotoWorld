using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reports",
                schema: "photoworld",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "open"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reports", x => x.id);
                    table.CheckConstraint("ck_reports_one_target", "(photo_id is null) <> (comment_id is null)");
                    table.CheckConstraint("ck_reports_reason", "reason in ('spam', 'nudity', 'harassment', 'violence', 'copyright', 'other')");
                    table.CheckConstraint("ck_reports_status", "status in ('open', 'dismissed', 'upheld')");
                    table.ForeignKey(
                        name: "fk_reports_photo_comments_comment_id",
                        column: x => x.comment_id,
                        principalSchema: "photoworld",
                        principalTable: "photo_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reports_photos_photo_id",
                        column: x => x.photo_id,
                        principalSchema: "photoworld",
                        principalTable: "photos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reports_users_reporter_id",
                        column: x => x.reporter_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reports_comment_id_status",
                schema: "photoworld",
                table: "reports",
                columns: new[] { "comment_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_photo_id_status",
                schema: "photoworld",
                table: "reports",
                columns: new[] { "photo_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_reporter_id_comment_id",
                schema: "photoworld",
                table: "reports",
                columns: new[] { "reporter_id", "comment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reports_reporter_id_photo_id",
                schema: "photoworld",
                table: "reports",
                columns: new[] { "reporter_id", "photo_id" },
                unique: true);

            // Stamp reviewed_at whenever a moderator changes a report's status in the Supabase table editor.
            migrationBuilder.Sql("""
                create or replace function photoworld.set_report_reviewed_at() returns trigger
                language plpgsql set search_path = '' as $$
                begin
                    if new.status is distinct from old.status then
                        new.reviewed_at := case when new.status = 'open' then null else now() end;
                    end if;
                    return new;
                end $$;

                create trigger reports_set_reviewed_at before update on photoworld.reports
                for each row execute function photoworld.set_report_reviewed_at();
                """);

            // Moderation queue for the Supabase table editor: open reports with what was reported and by whom.
            migrationBuilder.Sql("""
                create or replace view photoworld.open_reports with (security_invoker = true) as
                select r.id as report_id,
                       r.created_at,
                       case when r.photo_id is not null then 'photo' else 'comment' end as content_type,
                       coalesce(r.photo_id, c.photo_id) as photo_id,
                       r.comment_id,
                       coalesce(p.title, c.text) as content,
                       owner.display_name as content_owner,
                       r.reason,
                       r.note,
                       reporter.display_name as reporter,
                       (select count(*) from photoworld.reports o
                         where o.status = 'open'
                           and (o.photo_id = r.photo_id or o.comment_id = r.comment_id)) as open_reports_on_item
                from photoworld.reports r
                left join photoworld.photos p on p.id = r.photo_id
                left join photoworld.photo_comments c on c.id = r.comment_id
                left join photoworld.users owner on owner.id = coalesce(p.owner_id, c.author_id)
                join photoworld.users reporter on reporter.id = r.reporter_id
                where r.status = 'open'
                order by r.created_at desc;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("drop view if exists photoworld.open_reports;");
            migrationBuilder.Sql("drop function if exists photoworld.set_report_reviewed_at() cascade;");
            migrationBuilder.DropTable(
                name: "reports",
                schema: "photoworld");
        }
    }
}
