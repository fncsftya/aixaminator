using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aixaminator.Migrations
{
    /// <inheritdoc />
    public partial class UpdateNotesToHandleQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHighlight",
                table: "Notes");

            migrationBuilder.AddColumn<string>(
                name: "Answer",
                table: "Notes",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Answer",
                table: "Notes");

            migrationBuilder.AddColumn<bool>(
                name: "IsHighlight",
                table: "Notes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
