using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollabs : Migration
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
                name: "collab_id",
                schema: "photoworld",
                table: "reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "collabs",
                schema: "photoworld",
                table: "activity_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "collab_id",
                schema: "photoworld",
                table: "activities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "collabs",
                schema: "photoworld",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    role = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    terms = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    terms_note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    area = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    area_latitude = table.Column<double>(type: "double precision", nullable: false),
                    area_longitude = table.Column<double>(type: "double precision", nullable: false),
                    meeting_point_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    meeting_latitude = table.Column<double>(type: "double precision", nullable: false),
                    meeting_longitude = table.Column<double>(type: "double precision", nullable: false),
                    review_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "pending"),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reminder_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirm_request_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collabs", x => x.id);
                    table.CheckConstraint("ck_collabs_area", "area_latitude between -90 and 90 and area_longitude between -180 and 180");
                    table.CheckConstraint("ck_collabs_meeting_point", "meeting_latitude between -90 and 90 and meeting_longitude between -180 and 180");
                    table.CheckConstraint("ck_collabs_review_status", "review_status in ('pending', 'approved', 'rejected')");
                    table.CheckConstraint("ck_collabs_role", "role in ('model', 'makeup', 'second_shooter', 'assistant', 'photographer')");
                    table.CheckConstraint("ck_collabs_terms", "terms in ('paid', 'trade', 'unpaid')");
                    table.CheckConstraint("ck_collabs_times", "ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_collabs_users_poster_id",
                        column: x => x.poster_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "collab_messages",
                schema: "photoworld",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    collab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collab_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_collab_messages_collabs_collab_id",
                        column: x => x.collab_id,
                        principalSchema: "photoworld",
                        principalTable: "collabs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_collab_messages_users_author_id",
                        column: x => x.author_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "collab_photos",
                schema: "photoworld",
                columns: table => new
                {
                    collab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collab_photos", x => new { x.collab_id, x.photo_id });
                    table.ForeignKey(
                        name: "fk_collab_photos_collabs_collab_id",
                        column: x => x.collab_id,
                        principalSchema: "photoworld",
                        principalTable: "collabs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_collab_photos_photos_photo_id",
                        column: x => x.photo_id,
                        principalSchema: "photoworld",
                        principalTable: "photos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "collab_replies",
                schema: "photoworld",
                columns: table => new
                {
                    collab_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "pending"),
                    share_portfolio = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    poster_confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    member_confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collab_replies", x => new { x.collab_id, x.user_id });
                    table.CheckConstraint("ck_collab_replies_status", "status in ('pending', 'accepted', 'declined')");
                    table.ForeignKey(
                        name: "fk_collab_replies_collabs_collab_id",
                        column: x => x.collab_id,
                        principalSchema: "photoworld",
                        principalTable: "collabs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_collab_replies_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reports_collab_id",
                schema: "photoworld",
                table: "reports",
                column: "collab_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_reporter_id_collab_id",
                schema: "photoworld",
                table: "reports",
                columns: new[] { "reporter_id", "collab_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_reports_one_target",
                schema: "photoworld",
                table: "reports",
                sql: "num_nonnulls(photo_id, comment_id, blog_post_id, blog_comment_id, club_id, collab_id) = 1");

            migrationBuilder.CreateIndex(
                name: "ix_activities_collab_id",
                schema: "photoworld",
                table: "activities",
                column: "collab_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities",
                sql: "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result', 'photo_of_the_day', 'feedback', 'walk_new', 'walk_joined', 'walk_changed', 'walk_cancelled', 'walk_reminder', 'post_approved', 'post_rejected', 'photo_approved', 'photo_rejected', 'light_forecast', 'club_request', 'club_accepted', 'club_approved', 'club_rejected', 'club_photo', 'workshop_new', 'workshop_joined', 'workshop_changed', 'workshop_cancelled', 'workshop_reminder', 'collab_new', 'collab_reply', 'collab_accepted', 'collab_changed', 'collab_cancelled', 'collab_reminder', 'collab_confirm', 'collab_approved', 'collab_rejected')");

            migrationBuilder.CreateIndex(
                name: "ix_collab_messages_author_id",
                schema: "photoworld",
                table: "collab_messages",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_collab_messages_collab_id_id",
                schema: "photoworld",
                table: "collab_messages",
                columns: new[] { "collab_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_collab_photos_photo_id",
                schema: "photoworld",
                table: "collab_photos",
                column: "photo_id");

            migrationBuilder.CreateIndex(
                name: "ix_collab_replies_user_id",
                schema: "photoworld",
                table: "collab_replies",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_collabs_area_latitude",
                schema: "photoworld",
                table: "collabs",
                column: "area_latitude");

            migrationBuilder.CreateIndex(
                name: "ix_collabs_poster_id",
                schema: "photoworld",
                table: "collabs",
                column: "poster_id");

            migrationBuilder.CreateIndex(
                name: "ix_collabs_review_status_starts_at",
                schema: "photoworld",
                table: "collabs",
                columns: new[] { "review_status", "starts_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_activities_collabs_collab_id",
                schema: "photoworld",
                table: "activities",
                column: "collab_id",
                principalSchema: "photoworld",
                principalTable: "collabs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_reports_collabs_collab_id",
                schema: "photoworld",
                table: "reports",
                column: "collab_id",
                principalSchema: "photoworld",
                principalTable: "collabs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_activities_collabs_collab_id",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropForeignKey(
                name: "fk_reports_collabs_collab_id",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropTable(
                name: "collab_messages",
                schema: "photoworld");

            migrationBuilder.DropTable(
                name: "collab_photos",
                schema: "photoworld");

            migrationBuilder.DropTable(
                name: "collab_replies",
                schema: "photoworld");

            migrationBuilder.DropTable(
                name: "collabs",
                schema: "photoworld");

            migrationBuilder.DropIndex(
                name: "ix_reports_collab_id",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropIndex(
                name: "ix_reports_reporter_id_collab_id",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reports_one_target",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropIndex(
                name: "ix_activities_collab_id",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "collab_id",
                schema: "photoworld",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "collabs",
                schema: "photoworld",
                table: "activity_settings");

            migrationBuilder.DropColumn(
                name: "collab_id",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reports_one_target",
                schema: "photoworld",
                table: "reports",
                sql: "num_nonnulls(photo_id, comment_id, blog_post_id, blog_comment_id, club_id) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_activities_type",
                schema: "photoworld",
                table: "activities",
                sql: "type in ('like', 'comment', 'post_comment', 'follow', 'new_photo', 'new_post', 'challenge_open', 'challenge_ending', 'challenge_result', 'photo_of_the_day', 'feedback', 'walk_new', 'walk_joined', 'walk_changed', 'walk_cancelled', 'walk_reminder', 'post_approved', 'post_rejected', 'photo_approved', 'photo_rejected', 'light_forecast', 'club_request', 'club_accepted', 'club_approved', 'club_rejected', 'club_photo', 'workshop_new', 'workshop_joined', 'workshop_changed', 'workshop_cancelled', 'workshop_reminder')");
        }
    }
}
