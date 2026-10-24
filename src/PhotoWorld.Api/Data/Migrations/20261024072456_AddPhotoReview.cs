using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.AddColumn<string>(
                name: "review_note",
                schema: "photoworld",
                table: "photos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_status",
                schema: "photoworld",
                table: "photos",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "approved");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reviewed_at",
                schema: "photoworld",
                table: "photos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_photos_review_status_created_at",
                schema: "photoworld",
                table: "photos",
                columns: new[] { "review_status", "created_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_photos_review_status",
                schema: "photoworld",
                table: "photos",
                sql: "review_status in ('pending', 'approved', 'rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities",
                sql: "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result', 'photo_of_the_day', 'feedback', 'walk_new', 'walk_joined', 'walk_changed', 'walk_cancelled', 'walk_reminder', 'post_approved', 'post_rejected', 'photo_approved', 'photo_rejected')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_photos_review_status_created_at",
                schema: "photoworld",
                table: "photos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_photos_review_status",
                schema: "photoworld",
                table: "photos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "review_note",
                schema: "photoworld",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "review_status",
                schema: "photoworld",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                schema: "photoworld",
                table: "photos");

            // Rows the old constraint wouldn't allow. (Rolling back also makes every photo public again.)
            migrationBuilder.Sql("delete from photoworld.activities where type in ('photo_approved', 'photo_rejected');");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities",
                sql: "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result', 'photo_of_the_day', 'feedback', 'walk_new', 'walk_joined', 'walk_changed', 'walk_cancelled', 'walk_reminder', 'post_approved', 'post_rejected')");
        }
    }
}
