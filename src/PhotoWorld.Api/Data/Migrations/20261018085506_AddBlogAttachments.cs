using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoWorld.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBlogAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blog_attachments",
                schema: "photoworld",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_path = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blog_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_blog_attachments_blog_posts_post_id",
                        column: x => x.post_id,
                        principalSchema: "photoworld",
                        principalTable: "blog_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_blog_attachments_post_id_created_at",
                schema: "photoworld",
                table: "blog_attachments",
                columns: new[] { "post_id", "created_at" });

            // Public Supabase Storage bucket for PDFs attached to blog posts (20 MB each).
            migrationBuilder.Sql("""
                insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
                values ('attachments', 'attachments', true, 20971520, '{application/pdf}')
                on conflict (id) do nothing;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The bucket is left in place: Supabase refuses to drop a bucket that still holds files.
            migrationBuilder.DropTable(
                name: "blog_attachments",
                schema: "photoworld");
        }
    }
}
