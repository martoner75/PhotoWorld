using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClubCover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cover_photo_id",
                schema: "photoworld",
                table: "clubs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_clubs_cover_photo_id",
                schema: "photoworld",
                table: "clubs",
                column: "cover_photo_id");

            migrationBuilder.AddForeignKey(
                name: "fk_clubs_photos_cover_photo_id",
                schema: "photoworld",
                table: "clubs",
                column: "cover_photo_id",
                principalSchema: "photoworld",
                principalTable: "photos",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_clubs_photos_cover_photo_id",
                schema: "photoworld",
                table: "clubs");

            migrationBuilder.DropIndex(
                name: "ix_clubs_cover_photo_id",
                schema: "photoworld",
                table: "clubs");

            migrationBuilder.DropColumn(
                name: "cover_photo_id",
                schema: "photoworld",
                table: "clubs");
        }
    }
}
