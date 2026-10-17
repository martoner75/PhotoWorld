using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeShowcase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "show_on_home",
                schema: "photoworld",
                table: "photos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_photos_show_on_home_created_at",
                schema: "photoworld",
                table: "photos",
                columns: new[] { "show_on_home", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_photos_show_on_home_created_at",
                schema: "photoworld",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "show_on_home",
                schema: "photoworld",
                table: "photos");
        }
    }
}
