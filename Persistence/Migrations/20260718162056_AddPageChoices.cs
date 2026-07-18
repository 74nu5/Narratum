using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Narratum.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPageChoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SerializedChoices",
                table: "PageSnapshots",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SerializedChoices",
                table: "PageSnapshots");
        }
    }
}
