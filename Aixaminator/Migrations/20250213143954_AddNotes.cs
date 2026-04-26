using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aixaminator.Migrations
{
    /// <inheritdoc />
    public partial class AddNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentPartNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    Context = table.Column<string>(type: "TEXT", nullable: true),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    IsHighlight = table.Column<bool>(type: "INTEGER", nullable: false),
                    HighlightColour = table.Column<string>(type: "TEXT", nullable: true),
                    Location = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notes_DocumentParts_DocumentId_DocumentPartNumber",
                        columns: x => new { x.DocumentId, x.DocumentPartNumber },
                        principalTable: "DocumentParts",
                        principalColumns: new[] { "DocumentId", "PartNumber" });
                    table.ForeignKey(
                        name: "FK_Notes_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_DocumentId_DocumentPartNumber",
                table: "Notes",
                columns: new[] { "DocumentId", "DocumentPartNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notes");
        }
    }
}
