using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoPlaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "place_id",
                schema: "photoworld",
                table: "photos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_photos_place_id",
                schema: "photoworld",
                table: "photos",
                column: "place_id");

            migrationBuilder.AddForeignKey(
                name: "fk_photos_places_place_id",
                schema: "photoworld",
                table: "photos",
                column: "place_id",
                principalSchema: "photoworld",
                principalTable: "places",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_photos_places_place_id",
                schema: "photoworld",
                table: "photos");

            migrationBuilder.DropIndex(
                name: "ix_photos_place_id",
                schema: "photoworld",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "place_id",
                schema: "photoworld",
                table: "photos");
        }
    }
}
