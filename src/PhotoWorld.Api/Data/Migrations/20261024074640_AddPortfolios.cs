using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "portfolios",
                schema: "photoworld",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    bio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    website = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: true),
                    instagram = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolios", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_portfolios_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "portfolio_photos",
                schema: "photoworld",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolio_photos", x => new { x.user_id, x.photo_id });
                    table.ForeignKey(
                        name: "fk_portfolio_photos_photos_photo_id",
                        column: x => x.photo_id,
                        principalSchema: "photoworld",
                        principalTable: "photos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_portfolio_photos_portfolios_user_id",
                        column: x => x.user_id,
                        principalSchema: "photoworld",
                        principalTable: "portfolios",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_photos_photo_id",
                schema: "photoworld",
                table: "portfolio_photos",
                column: "photo_id");

            migrationBuilder.CreateIndex(
                name: "ix_portfolios_slug",
                schema: "photoworld",
                table: "portfolios",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "portfolio_photos",
                schema: "photoworld");

            migrationBuilder.DropTable(
                name: "portfolios",
                schema: "photoworld");
        }
    }
}
