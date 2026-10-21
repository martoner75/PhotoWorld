using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoWalks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.AddColumn<bool>(
                name: "walks",
                schema: "photoworld",
                table: "activity_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "walk_id",
                schema: "photoworld",
                table: "activities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "walks",
                schema: "photoworld",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    meeting_point = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    max_people = table.Column<int>(type: "integer", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reminder_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_walks", x => x.id);
                    table.CheckConstraint("ck_walks_latitude", "latitude between -90 and 90");
                    table.CheckConstraint("ck_walks_longitude", "longitude between -180 and 180");
                    table.CheckConstraint("ck_walks_times", "ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_walks_users_organizer_id",
                        column: x => x.organizer_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "walk_attendees",
                schema: "photoworld",
                columns: table => new
                {
                    walk_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_walk_attendees", x => new { x.walk_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_walk_attendees_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_walk_attendees_walks_walk_id",
                        column: x => x.walk_id,
                        principalSchema: "photoworld",
                        principalTable: "walks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activities_walk_id",
                schema: "photoworld",
                table: "activities",
                column: "walk_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities",
                sql: "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result', 'photo_of_the_day', 'feedback', 'walk_new', 'walk_joined', 'walk_changed', 'walk_cancelled', 'walk_reminder')");

            migrationBuilder.CreateIndex(
                name: "ix_walk_attendees_user_id",
                schema: "photoworld",
                table: "walk_attendees",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_walks_organizer_id",
                schema: "photoworld",
                table: "walks",
                column: "organizer_id");

            migrationBuilder.CreateIndex(
                name: "ix_walks_starts_at",
                schema: "photoworld",
                table: "walks",
                column: "starts_at");

            migrationBuilder.AddForeignKey(
                name: "fk_activities_walks_walk_id",
                schema: "photoworld",
                table: "activities",
                column: "walk_id",
                principalSchema: "photoworld",
                principalTable: "walks",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_activities_walks_walk_id",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropTable(
                name: "walk_attendees",
                schema: "photoworld");

            migrationBuilder.DropTable(
                name: "walks",
                schema: "photoworld");

            migrationBuilder.DropIndex(
                name: "ix_activities_walk_id",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "walks",
                schema: "photoworld",
                table: "activity_settings");

            migrationBuilder.DropColumn(
                name: "walk_id",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities",
                sql: "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result', 'photo_of_the_day', 'feedback')");
        }
    }
}
