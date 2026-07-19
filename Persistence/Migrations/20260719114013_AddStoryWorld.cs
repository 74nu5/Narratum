using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Narratum.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoryWorld : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SerializedWorld",
                table: "SaveSlots",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SerializedWorld",
                table: "SaveSlots");
        }
    }
}
