using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAdminAndSuspension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_admin",
                schema: "photoworld",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "suspended_at",
                schema: "photoworld",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suspension_reason",
                schema: "photoworld",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            // The first admin. Further admins are made on the website's Admin → Users page.
            migrationBuilder.Sql("update photoworld.users set is_admin = true where lower(email) = 'rosario.martone@gmail.com';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_admin",
                schema: "photoworld",
                table: "users");

            migrationBuilder.DropColumn(
                name: "suspended_at",
                schema: "photoworld",
                table: "users");

            migrationBuilder.DropColumn(
                name: "suspension_reason",
                schema: "photoworld",
                table: "users");
        }
    }
}
