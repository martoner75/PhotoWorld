using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClubs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_reports_one_target",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.AddColumn<Guid>(
                name: "club_id",
                schema: "photoworld",
                table: "reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "clubs",
                schema: "photoworld",
                table: "activity_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "club_id",
                schema: "photoworld",
                table: "activities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "clubs",
                schema: "photoworld",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    city = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    join_policy = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    review_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "pending"),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clubs", x => x.id);
                    table.CheckConstraint("ck_clubs_area", "(latitude is null) = (longitude is null)");
                    table.CheckConstraint("ck_clubs_join_policy", "join_policy in ('open', 'approval')");
                    table.CheckConstraint("ck_clubs_review_status", "review_status in ('pending', 'approved', 'rejected')");
                    table.ForeignKey(
                        name: "fk_clubs_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "club_members",
                schema: "photoworld",
                columns: table => new
                {
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_club_members", x => new { x.club_id, x.user_id });
                    table.CheckConstraint("ck_club_members_role", "role in ('owner', 'admin', 'member')");
                    table.CheckConstraint("ck_club_members_status", "status in ('active', 'pending')");
                    table.ForeignKey(
                        name: "fk_club_members_clubs_club_id",
                        column: x => x.club_id,
                        principalSchema: "photoworld",
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_club_members_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reports_club_id_status",
                schema: "photoworld",
                table: "reports",
                columns: new[] { "club_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_reporter_id_club_id",
                schema: "photoworld",
                table: "reports",
                columns: new[] { "reporter_id", "club_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_reports_one_target",
                schema: "photoworld",
                table: "reports",
                sql: "num_nonnulls(photo_id, comment_id, blog_post_id, blog_comment_id, club_id) = 1");

            migrationBuilder.CreateIndex(
                name: "ix_activities_club_id",
                schema: "photoworld",
                table: "activities",
                column: "club_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities",
                sql: "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result', 'photo_of_the_day', 'feedback', 'walk_new', 'walk_joined', 'walk_changed', 'walk_cancelled', 'walk_reminder', 'post_approved', 'post_rejected', 'photo_approved', 'photo_rejected', 'light_forecast', 'club_request', 'club_accepted', 'club_approved', 'club_rejected')");

            migrationBuilder.CreateIndex(
                name: "ix_club_members_one_owner",
                schema: "photoworld",
                table: "club_members",
                column: "club_id",
                unique: true,
                filter: "role = 'owner'");

            migrationBuilder.CreateIndex(
                name: "ix_club_members_user_id",
                schema: "photoworld",
                table: "club_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_clubs_owner_id",
                schema: "photoworld",
                table: "clubs",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_clubs_review_status_created_at",
                schema: "photoworld",
                table: "clubs",
                columns: new[] { "review_status", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_activities_clubs_club_id",
                schema: "photoworld",
                table: "activities",
                column: "club_id",
                principalSchema: "photoworld",
                principalTable: "clubs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_reports_clubs_club_id",
                schema: "photoworld",
                table: "reports",
                column: "club_id",
                principalSchema: "photoworld",
                principalTable: "clubs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // The moderation queue in the Supabase dashboard now also lists reported clubs.
            migrationBuilder.Sql("""
                drop view if exists photoworld.open_reports;
                create view photoworld.open_reports with (security_invoker = true) as
                select r.id as report_id,
                       r.created_at,
                       case when r.photo_id is not null then 'photo'
                            when r.comment_id is not null then 'photo_comment'
                            when r.blog_post_id is not null then 'blog_post'
                            when r.blog_comment_id is not null then 'blog_comment'
                            else 'club' end as content_type,
                       coalesce(r.photo_id, c.photo_id) as photo_id,
                       r.comment_id,
                       coalesce(r.blog_post_id, bc.post_id) as blog_post_id,
                       r.blog_comment_id,
                       r.club_id,
                       coalesce(p.title, c.text, bp.title, bc.text, cl.name) as content,
                       owner.display_name as content_owner,
                       r.reason,
                       r.note,
                       reporter.display_name as reporter,
                       (select count(*) from photoworld.reports o
                         where o.status = 'open'
                           and (o.photo_id = r.photo_id or o.comment_id = r.comment_id
                                or o.blog_post_id = r.blog_post_id or o.blog_comment_id = r.blog_comment_id
                                or o.club_id = r.club_id)) as open_reports_on_item
                from photoworld.reports r
                left join photoworld.photos p on p.id = r.photo_id
                left join photoworld.photo_comments c on c.id = r.comment_id
                left join photoworld.blog_posts bp on bp.id = r.blog_post_id
                left join photoworld.blog_comments bc on bc.id = r.blog_comment_id
                left join photoworld.clubs cl on cl.id = r.club_id
                left join photoworld.users owner on owner.id = coalesce(p.owner_id, c.author_id, bp.author_id, bc.author_id, cl.owner_id)
                join photoworld.users reporter on reporter.id = r.reporter_id
                where r.status = 'open'
                order by r.created_at desc;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The view uses reports.club_id; drop it first and restore the AddBlogs version at the end.
            migrationBuilder.Sql("delete from photoworld.reports where club_id is not null;");
            migrationBuilder.Sql("drop view if exists photoworld.open_reports;");
            migrationBuilder.Sql("delete from photoworld.activities where type like 'club_%';");

            migrationBuilder.DropForeignKey(
                name: "fk_activities_clubs_club_id",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropForeignKey(
                name: "fk_reports_clubs_club_id",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropTable(
                name: "club_members",
                schema: "photoworld");

            migrationBuilder.DropTable(
                name: "clubs",
                schema: "photoworld");

            migrationBuilder.DropIndex(
                name: "ix_reports_club_id_status",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropIndex(
                name: "ix_reports_reporter_id_club_id",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reports_one_target",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropIndex(
                name: "ix_activities_club_id",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "club_id",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "clubs",
                schema: "photoworld",
                table: "activity_settings");

            migrationBuilder.DropColumn(
                name: "club_id",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reports_one_target",
                schema: "photoworld",
                table: "reports",
                sql: "num_nonnulls(photo_id, comment_id, blog_post_id, blog_comment_id) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities",
                sql: "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result', 'photo_of_the_day', 'feedback', 'walk_new', 'walk_joined', 'walk_changed', 'walk_cancelled', 'walk_reminder', 'post_approved', 'post_rejected', 'photo_approved', 'photo_rejected', 'light_forecast')");

            migrationBuilder.Sql("""
                create view photoworld.open_reports with (security_invoker = true) as
                select r.id as report_id,
                       r.created_at,
                       case when r.photo_id is not null then 'photo'
                            when r.comment_id is not null then 'photo_comment'
                            when r.blog_post_id is not null then 'blog_post'
                            else 'blog_comment' end as content_type,
                       coalesce(r.photo_id, c.photo_id) as photo_id,
                       r.comment_id,
                       coalesce(r.blog_post_id, bc.post_id) as blog_post_id,
                       r.blog_comment_id,
                       coalesce(p.title, c.text, bp.title, bc.text) as content,
                       owner.display_name as content_owner,
                       r.reason,
                       r.note,
                       reporter.display_name as reporter,
                       (select count(*) from photoworld.reports o
                         where o.status = 'open'
                           and (o.photo_id = r.photo_id or o.comment_id = r.comment_id
                                or o.blog_post_id = r.blog_post_id or o.blog_comment_id = r.blog_comment_id)) as open_reports_on_item
                from photoworld.reports r
                left join photoworld.photos p on p.id = r.photo_id
                left join photoworld.photo_comments c on c.id = r.comment_id
                left join photoworld.blog_posts bp on bp.id = r.blog_post_id
                left join photoworld.blog_comments bc on bc.id = r.blog_comment_id
                left join photoworld.users owner on owner.id = coalesce(p.owner_id, c.author_id, bp.author_id, bc.author_id)
                join photoworld.users reporter on reporter.id = r.reporter_id
                where r.status = 'open'
                order by r.created_at desc;
                """);
        }
    }
}
