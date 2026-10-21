using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ending_notified_at",
                schema: "photoworld",
                table: "challenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "open_notified_at",
                schema: "photoworld",
                table: "challenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "results_notified_at",
                schema: "photoworld",
                table: "challenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "activities",
                schema: "photoworld",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    photo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    challenge_id = table.Column<Guid>(type: "uuid", nullable: true),
                    place = table.Column<int>(type: "integer", nullable: true),
                    snippet = table.Column<string>(type: "character varying(141)", maxLength: 141, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activities", x => x.id);
                    table.CheckConstraint("ck_activities_type", "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result')");
                    table.ForeignKey(
                        name: "fk_activities_blog_posts_post_id",
                        column: x => x.post_id,
                        principalSchema: "photoworld",
                        principalTable: "blog_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_activities_challenges_challenge_id",
                        column: x => x.challenge_id,
                        principalSchema: "photoworld",
                        principalTable: "challenges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_activities_photos_photo_id",
                        column: x => x.photo_id,
                        principalSchema: "photoworld",
                        principalTable: "photos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_activities_users_actor_id",
                        column: x => x.actor_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_activities_users_recipient_id",
                        column: x => x.recipient_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activity_settings",
                schema: "photoworld",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    likes = table.Column<bool>(type: "boolean", nullable: false),
                    comments = table.Column<bool>(type: "boolean", nullable: false),
                    follows = table.Column<bool>(type: "boolean", nullable: false),
                    followed_posts = table.Column<bool>(type: "boolean", nullable: false),
                    challenges = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activity_settings", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_activity_settings_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activities_actor_id",
                schema: "photoworld",
                table: "activities",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_activities_challenge_id",
                schema: "photoworld",
                table: "activities",
                column: "challenge_id");

            migrationBuilder.CreateIndex(
                name: "ix_activities_created_at",
                schema: "photoworld",
                table: "activities",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_activities_photo_id",
                schema: "photoworld",
                table: "activities",
                column: "photo_id");

            migrationBuilder.CreateIndex(
                name: "ix_activities_post_id",
                schema: "photoworld",
                table: "activities",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "ix_activities_recipient_id_created_at",
                schema: "photoworld",
                table: "activities",
                columns: new[] { "recipient_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_activities_recipient_id_read_at",
                schema: "photoworld",
                table: "activities",
                columns: new[] { "recipient_id", "read_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activities",
                schema: "photoworld");

            migrationBuilder.DropTable(
                name: "activity_settings",
                schema: "photoworld");

            migrationBuilder.DropColumn(
                name: "ending_notified_at",
                schema: "photoworld",
                table: "challenges");

            migrationBuilder.DropColumn(
                name: "open_notified_at",
                schema: "photoworld",
                table: "challenges");

            migrationBuilder.DropColumn(
                name: "results_notified_at",
                schema: "photoworld",
                table: "challenges");
        }
    }
}
