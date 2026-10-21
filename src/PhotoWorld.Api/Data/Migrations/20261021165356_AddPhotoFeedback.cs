using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.AddColumn<bool>(
                name: "wants_feedback",
                schema: "photoworld",
                table: "photos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "photo_feedback",
                schema: "photoworld",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    composition = table.Column<int>(type: "integer", nullable: false),
                    light = table.Column<int>(type: "integer", nullable: false),
                    focus = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photo_feedback", x => x.id);
                    table.CheckConstraint("ck_photo_feedback_ratings", "composition between 1 and 5 and light between 1 and 5 and focus between 1 and 5");
                    table.ForeignKey(
                        name: "fk_photo_feedback_photos_photo_id",
                        column: x => x.photo_id,
                        principalSchema: "photoworld",
                        principalTable: "photos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_photo_feedback_users_author_id",
                        column: x => x.author_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities",
                sql: "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result', 'photo_of_the_day', 'feedback')");

            migrationBuilder.CreateIndex(
                name: "ix_photo_feedback_author_id",
                schema: "photoworld",
                table: "photo_feedback",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_photo_feedback_photo_id_author_id",
                schema: "photoworld",
                table: "photo_feedback",
                columns: new[] { "photo_id", "author_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "photo_feedback",
                schema: "photoworld");

            migrationBuilder.DropCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "wants_feedback",
                schema: "photoworld",
                table: "photos");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities",
                sql: "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result', 'photo_of_the_day')");
        }
    }
}
