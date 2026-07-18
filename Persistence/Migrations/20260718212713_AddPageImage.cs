using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Narratum.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPageImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "PageSnapshots",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagePrompt",
                table: "PageSnapshots",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "PageSnapshots");

            migrationBuilder.DropColumn(
                name: "ImagePrompt",
                table: "PageSnapshots");
        }
    }
}
