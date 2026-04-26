using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aixaminator.Migrations
{
    /// <inheritdoc />
    public partial class PartsReadAsList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentSection",
                table: "Documents");

            migrationBuilder.AddColumn<string>(
                name: "PartsRead",
                table: "Documents",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PartsRead",
                table: "Documents");

            migrationBuilder.AddColumn<int>(
                name: "CurrentSection",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
