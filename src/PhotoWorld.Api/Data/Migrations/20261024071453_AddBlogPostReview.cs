using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBlogPostReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_blog_posts_status",
                schema: "photoworld",
                table: "blog_posts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.AddColumn<string>(
                name: "review_note",
                schema: "photoworld",
                table: "blog_posts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reviewed_at",
                schema: "photoworld",
                table: "blog_posts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_blog_posts_status",
                schema: "photoworld",
                table: "blog_posts",
                sql: "status in ('draft', 'pending', 'published', 'rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities",
                sql: "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result', 'photo_of_the_day', 'feedback', 'walk_new', 'walk_joined', 'walk_changed', 'walk_cancelled', 'walk_reminder', 'post_approved', 'post_rejected')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_blog_posts_status",
                schema: "photoworld",
                table: "blog_posts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "review_note",
                schema: "photoworld",
                table: "blog_posts");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                schema: "photoworld",
                table: "blog_posts");

            // Rows the old constraints wouldn't allow: posts in or back from review become drafts again.
            migrationBuilder.Sql("update photoworld.blog_posts set status = 'draft' where status in ('pending', 'rejected');");
            migrationBuilder.Sql("delete from photoworld.activities where type in ('post_approved', 'post_rejected');");

            migrationBuilder.AddCheckConstraint(
                name: "ck_blog_posts_status",
                schema: "photoworld",
                table: "blog_posts",
                sql: "status in ('draft', 'published')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities",
                sql: "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result', 'photo_of_the_day', 'feedback', 'walk_new', 'walk_joined', 'walk_changed', 'walk_cancelled', 'walk_reminder')");
        }
    }
}
