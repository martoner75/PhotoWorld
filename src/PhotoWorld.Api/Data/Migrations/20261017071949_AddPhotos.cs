using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "photos",
                schema: "photoworld",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    display_path = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    thumbnail_path = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    camera_make = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    camera_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lens = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    focal_length_mm = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    aperture = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    exposure_time = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    iso = table.Column<int>(type: "integer", nullable: true),
                    taken_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_photos_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "photo_comments",
                schema: "photoworld",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photo_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_photo_comments_photos_photo_id",
                        column: x => x.photo_id,
                        principalSchema: "photoworld",
                        principalTable: "photos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_photo_comments_users_author_id",
                        column: x => x.author_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "photo_likes",
                schema: "photoworld",
                columns: table => new
                {
                    photo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photo_likes", x => new { x.photo_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_photo_likes_photos_photo_id",
                        column: x => x.photo_id,
                        principalSchema: "photoworld",
                        principalTable: "photos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_photo_likes_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "photoworld",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_photo_comments_author_id",
                schema: "photoworld",
                table: "photo_comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_photo_comments_photo_id_created_at",
                schema: "photoworld",
                table: "photo_comments",
                columns: new[] { "photo_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_photo_likes_user_id",
                schema: "photoworld",
                table: "photo_likes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_photos_created_at",
                schema: "photoworld",
                table: "photos",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_photos_owner_id_created_at",
                schema: "photoworld",
                table: "photos",
                columns: new[] { "owner_id", "created_at" },
                descending: new[] { false, true });

            // Public Supabase Storage bucket for the re-encoded JPEGs (originals are never stored).
            migrationBuilder.Sql("""
                insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
                values ('photos', 'photos', true, 10485760, '{image/jpeg}')
                on conflict (id) do nothing;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The bucket is left in place: Supabase refuses to drop a bucket that still holds files.
            migrationBuilder.DropTable(
                name: "photo_comments",
                schema: "photoworld");

            migrationBuilder.DropTable(
                name: "photo_likes",
                schema: "photoworld");

            migrationBuilder.DropTable(
                name: "photos",
                schema: "photoworld");
        }
    }
}
