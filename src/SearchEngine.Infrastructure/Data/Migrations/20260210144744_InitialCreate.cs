using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SearchEngine.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Views = table.Column<int>(type: "integer", nullable: true),
                    Likes = table.Column<int>(type: "integer", nullable: true),
                    Duration = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReadingTime = table.Column<int>(type: "integer", nullable: true),
                    Reactions = table.Column<int>(type: "integer", nullable: true),
                    Comments = table.Column<int>(type: "integer", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    FinalScore = table.Column<double>(type: "double precision", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contents_ContentType",
                table: "Contents",
                column: "ContentType");

            migrationBuilder.CreateIndex(
                name: "IX_Contents_ExternalId_SourceProvider",
                table: "Contents",
                columns: new[] { "ExternalId", "SourceProvider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contents_FinalScore",
                table: "Contents",
                column: "FinalScore",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Contents_PublishedAt",
                table: "Contents",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Contents_Title",
                table: "Contents",
                column: "Title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contents");
        }
    }
}
