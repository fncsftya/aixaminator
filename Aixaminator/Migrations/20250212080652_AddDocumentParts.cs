using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aixaminator.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentParts",
                columns: table => new
                {
                    PartNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UseColour = table.Column<bool>(type: "INTEGER", nullable: false),
                    Colour = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Hidden = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentParts", x => new { x.DocumentId, x.PartNumber });
                    table.ForeignKey(
                        name: "FK_DocumentParts_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentParts");
        }
    }
}
