using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClubWalksAndChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "club_id",
                schema: "photoworld",
                table: "walks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "club_id",
                schema: "photoworld",
                table: "challenges",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_walks_club_id",
                schema: "photoworld",
                table: "walks",
                column: "club_id");

            migrationBuilder.CreateIndex(
                name: "ix_challenges_club_id",
                schema: "photoworld",
                table: "challenges",
                column: "club_id");

            migrationBuilder.AddForeignKey(
                name: "fk_challenges_clubs_club_id",
                schema: "photoworld",
                table: "challenges",
                column: "club_id",
                principalSchema: "photoworld",
                principalTable: "clubs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_walks_clubs_club_id",
                schema: "photoworld",
                table: "walks",
                column: "club_id",
                principalSchema: "photoworld",
                principalTable: "clubs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Without club_id these would turn public (members-only meeting points included): remove them instead.
            migrationBuilder.Sql("delete from photoworld.walks where club_id is not null;");
            migrationBuilder.Sql("delete from photoworld.challenges where club_id is not null;");

            migrationBuilder.DropForeignKey(
                name: "fk_challenges_clubs_club_id",
                schema: "photoworld",
                table: "challenges");

            migrationBuilder.DropForeignKey(
                name: "fk_walks_clubs_club_id",
                schema: "photoworld",
                table: "walks");

            migrationBuilder.DropIndex(
                name: "ix_walks_club_id",
                schema: "photoworld",
                table: "walks");

            migrationBuilder.DropIndex(
                name: "ix_challenges_club_id",
                schema: "photoworld",
                table: "challenges");

            migrationBuilder.DropColumn(
                name: "club_id",
                schema: "photoworld",
                table: "walks");

            migrationBuilder.DropColumn(
                name: "club_id",
                schema: "photoworld",
                table: "challenges");
        }
    }
}
