using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPushDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "push",
                schema: "photoworld",
                table: "activity_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "pushed_at",
                schema: "photoworld",
                table: "activities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "devices",
                schema: "photoworld",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_devices", x => x.id);
                    table.CheckConstraint("ck_devices_platform", "platform in ('ios', 'android')");
                    table.ForeignKey(
                        name: "fk_devices_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activities_pushed_at",
                schema: "photoworld",
                table: "activities",
                column: "pushed_at");

            migrationBuilder.CreateIndex(
                name: "ix_devices_token",
                schema: "photoworld",
                table: "devices",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_devices_user_id",
                schema: "photoworld",
                table: "devices",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "devices",
                schema: "photoworld");

            migrationBuilder.DropIndex(
                name: "ix_activities_pushed_at",
                schema: "photoworld",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "push",
                schema: "photoworld",
                table: "activity_settings");

            migrationBuilder.DropColumn(
                name: "pushed_at",
                schema: "photoworld",
                table: "activities");
        }
    }
}
