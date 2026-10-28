using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkshops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.AddColumn<bool>(
                name: "workshops",
                schema: "photoworld",
                table: "activity_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "workshop_id",
                schema: "photoworld",
                table: "activities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "workshops",
                schema: "photoworld",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    video_link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    max_people = table.Column<int>(type: "integer", nullable: true),
                    club_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reminder_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workshops", x => x.id);
                    table.CheckConstraint("ck_workshops_times", "ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_workshops_clubs_club_id",
                        column: x => x.club_id,
                        principalSchema: "photoworld",
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workshops_users_host_id",
                        column: x => x.host_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workshop_attendees",
                schema: "photoworld",
                columns: table => new
                {
                    workshop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workshop_attendees", x => new { x.workshop_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_workshop_attendees_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workshop_attendees_workshops_workshop_id",
                        column: x => x.workshop_id,
                        principalSchema: "photoworld",
                        principalTable: "workshops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workshop_messages",
                schema: "photoworld",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    workshop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workshop_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_workshop_messages_users_author_id",
                        column: x => x.author_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workshop_messages_workshops_workshop_id",
                        column: x => x.workshop_id,
                        principalSchema: "photoworld",
                        principalTable: "workshops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activities_workshop_id",
                schema: "photoworld",
                table: "activities",
                column: "workshop_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities",
                sql: "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result', 'photo_of_the_day', 'feedback', 'walk_new', 'walk_joined', 'walk_changed', 'walk_cancelled', 'walk_reminder', 'post_approved', 'post_rejected', 'photo_approved', 'photo_rejected', 'light_forecast', 'club_request', 'club_accepted', 'club_approved', 'club_rejected', 'club_photo', 'workshop_new', 'workshop_joined', 'workshop_changed', 'workshop_cancelled', 'workshop_reminder')");

            migrationBuilder.CreateIndex(
                name: "ix_workshop_attendees_user_id",
                schema: "photoworld",
                table: "workshop_attendees",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_workshop_messages_author_id",
                schema: "photoworld",
                table: "workshop_messages",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_workshop_messages_workshop_id_id",
                schema: "photoworld",
                table: "workshop_messages",
                columns: new[] { "workshop_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_workshops_club_id",
                schema: "photoworld",
                table: "workshops",
                column: "club_id");

            migrationBuilder.CreateIndex(
                name: "ix_workshops_host_id",
                schema: "photoworld",
                table: "workshops",
                column: "host_id");

            migrationBuilder.CreateIndex(
                name: "ix_workshops_starts_at",
                schema: "photoworld",
                table: "workshops",
                column: "starts_at");

            migrationBuilder.AddForeignKey(
                name: "fk_activities_workshops_workshop_id",
                schema: "photoworld",
                table: "activities",
                column: "workshop_id",
                principalSchema: "photoworld",
                principalTable: "workshops",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_activities_workshops_workshop_id",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropTable(
                name: "workshop_attendees",
                schema: "photoworld");

            migrationBuilder.DropTable(
                name: "workshop_messages",
                schema: "photoworld");

            migrationBuilder.DropTable(
                name: "workshops",
                schema: "photoworld");

            migrationBuilder.DropIndex(
                name: "ix_activities_workshop_id",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "workshops",
                schema: "photoworld",
                table: "activity_settings");

            migrationBuilder.DropColumn(
                name: "workshop_id",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities",
                sql: "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result', 'photo_of_the_day', 'feedback', 'walk_new', 'walk_joined', 'walk_changed', 'walk_cancelled', 'walk_reminder', 'post_approved', 'post_rejected', 'photo_approved', 'photo_rejected', 'light_forecast', 'club_request', 'club_accepted', 'club_approved', 'club_rejected', 'club_photo')");
        }
    }
}
