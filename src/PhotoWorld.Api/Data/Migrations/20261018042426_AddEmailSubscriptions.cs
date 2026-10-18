using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_subscriptions",
                schema: "photoworld",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    frequency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirm_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    confirm_token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    unsubscribe_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    notified_through = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    period_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_subscriptions", x => x.user_id);
                    table.CheckConstraint("ck_email_subscriptions_frequency", "frequency in ('instant', 'daily', 'weekly')");
                    table.ForeignKey(
                        name: "fk_email_subscriptions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_email_subscriptions_confirm_token_hash",
                schema: "photoworld",
                table: "email_subscriptions",
                column: "confirm_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_subscriptions_unsubscribe_token",
                schema: "photoworld",
                table: "email_subscriptions",
                column: "unsubscribe_token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_subscriptions",
                schema: "photoworld");
        }
    }
}
