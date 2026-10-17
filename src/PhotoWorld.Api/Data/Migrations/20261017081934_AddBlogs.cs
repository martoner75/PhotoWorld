using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBlogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_reports_one_target",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.AddColumn<Guid>(
                name: "blog_comment_id",
                schema: "photoworld",
                table: "reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "blog_post_id",
                schema: "photoworld",
                table: "reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "blog_posts",
                schema: "photoworld",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    body_html = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    excerpt = table.Column<string>(type: "character varying(290)", maxLength: 290, nullable: false),
                    cover_photo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blog_posts", x => x.id);
                    table.CheckConstraint("ck_blog_posts_status", "status in ('draft', 'published')");
                    table.ForeignKey(
                        name: "fk_blog_posts_photos_cover_photo_id",
                        column: x => x.cover_photo_id,
                        principalSchema: "photoworld",
                        principalTable: "photos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_blog_posts_users_author_id",
                        column: x => x.author_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "blogs",
                schema: "photoworld",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    intro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blogs", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_blogs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "blog_comments",
                schema: "photoworld",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blog_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_blog_comments_blog_posts_post_id",
                        column: x => x.post_id,
                        principalSchema: "photoworld",
                        principalTable: "blog_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_blog_comments_users_author_id",
                        column: x => x.author_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "blog_post_photos",
                schema: "photoworld",
                columns: table => new
                {
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blog_post_photos", x => new { x.post_id, x.photo_id });
                    table.ForeignKey(
                        name: "fk_blog_post_photos_blog_posts_post_id",
                        column: x => x.post_id,
                        principalSchema: "photoworld",
                        principalTable: "blog_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_blog_post_photos_photos_photo_id",
                        column: x => x.photo_id,
                        principalSchema: "photoworld",
                        principalTable: "photos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reports_blog_comment_id_status",
                schema: "photoworld",
                table: "reports",
                columns: new[] { "blog_comment_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_blog_post_id_status",
                schema: "photoworld",
                table: "reports",
                columns: new[] { "blog_post_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_reporter_id_blog_comment_id",
                schema: "photoworld",
                table: "reports",
                columns: new[] { "reporter_id", "blog_comment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reports_reporter_id_blog_post_id",
                schema: "photoworld",
                table: "reports",
                columns: new[] { "reporter_id", "blog_post_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_reports_one_target",
                schema: "photoworld",
                table: "reports",
                sql: "num_nonnulls(photo_id, comment_id, blog_post_id, blog_comment_id) = 1");

            migrationBuilder.CreateIndex(
                name: "ix_blog_comments_author_id",
                schema: "photoworld",
                table: "blog_comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_blog_comments_post_id_created_at",
                schema: "photoworld",
                table: "blog_comments",
                columns: new[] { "post_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_blog_post_photos_photo_id",
                schema: "photoworld",
                table: "blog_post_photos",
                column: "photo_id");

            migrationBuilder.CreateIndex(
                name: "ix_blog_posts_author_id_updated_at",
                schema: "photoworld",
                table: "blog_posts",
                columns: new[] { "author_id", "updated_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_blog_posts_cover_photo_id",
                schema: "photoworld",
                table: "blog_posts",
                column: "cover_photo_id");

            migrationBuilder.CreateIndex(
                name: "ix_blog_posts_status_published_at",
                schema: "photoworld",
                table: "blog_posts",
                columns: new[] { "status", "published_at" },
                descending: new[] { false, true });

            migrationBuilder.AddForeignKey(
                name: "fk_reports_blog_comments_blog_comment_id",
                schema: "photoworld",
                table: "reports",
                column: "blog_comment_id",
                principalSchema: "photoworld",
                principalTable: "blog_comments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_reports_blog_posts_blog_post_id",
                schema: "photoworld",
                table: "reports",
                column: "blog_post_id",
                principalSchema: "photoworld",
                principalTable: "blog_posts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // The moderation queue now also lists blog posts and blog comments.
            migrationBuilder.Sql("""
                drop view if exists photoworld.open_reports;
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The view depends on the blog columns; drop it first and restore the photos-only version at the end.
            migrationBuilder.Sql("drop view if exists photoworld.open_reports;");
            migrationBuilder.DropForeignKey(
                name: "fk_reports_blog_comments_blog_comment_id",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropForeignKey(
                name: "fk_reports_blog_posts_blog_post_id",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropTable(
                name: "blog_comments",
                schema: "photoworld");

            migrationBuilder.DropTable(
                name: "blog_post_photos",
                schema: "photoworld");

            migrationBuilder.DropTable(
                name: "blogs",
                schema: "photoworld");

            migrationBuilder.DropTable(
                name: "blog_posts",
                schema: "photoworld");

            migrationBuilder.DropIndex(
                name: "ix_reports_blog_comment_id_status",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropIndex(
                name: "ix_reports_blog_post_id_status",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropIndex(
                name: "ix_reports_reporter_id_blog_comment_id",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropIndex(
                name: "ix_reports_reporter_id_blog_post_id",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reports_one_target",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "blog_comment_id",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "blog_post_id",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reports_one_target",
                schema: "photoworld",
                table: "reports",
                sql: "(photo_id is null) <> (comment_id is null)");

            migrationBuilder.Sql("""
                drop view if exists photoworld.open_reports;
                create view photoworld.open_reports with (security_invoker = true) as
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
    }
}
