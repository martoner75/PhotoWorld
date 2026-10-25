using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClubPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.AddColumn<bool>(
                name: "club_photos",
                schema: "photoworld",
                table: "activity_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "club_photos",
                schema: "photoworld",
                columns: table => new
                {
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_club_photos", x => new { x.club_id, x.photo_id });
                    table.ForeignKey(
                        name: "fk_club_photos_clubs_club_id",
                        column: x => x.club_id,
                        principalSchema: "photoworld",
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_club_photos_photos_photo_id",
                        column: x => x.photo_id,
                        principalSchema: "photoworld",
                        principalTable: "photos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities",
                sql: "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result', 'photo_of_the_day', 'feedback', 'walk_new', 'walk_joined', 'walk_changed', 'walk_cancelled', 'walk_reminder', 'post_approved', 'post_rejected', 'photo_approved', 'photo_rejected', 'light_forecast', 'club_request', 'club_accepted', 'club_approved', 'club_rejected', 'club_photo')");

            migrationBuilder.CreateIndex(
                name: "ix_club_photos_club_id_shared_at",
                schema: "photoworld",
                table: "club_photos",
                columns: new[] { "club_id", "shared_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_club_photos_photo_id",
                schema: "photoworld",
                table: "club_photos",
                column: "photo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("delete from photoworld.activities where type = 'club_photo';");

            migrationBuilder.DropTable(
                name: "club_photos",
                schema: "photoworld");

            migrationBuilder.DropCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "club_photos",
                schema: "photoworld",
                table: "activity_settings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities",
                sql: "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result', 'photo_of_the_day', 'feedback', 'walk_new', 'walk_joined', 'walk_changed', 'walk_cancelled', 'walk_reminder', 'post_approved', 'post_rejected', 'photo_approved', 'photo_rejected', 'light_forecast', 'club_request', 'club_accepted', 'club_approved', 'club_rejected')");
        }
    }
}
