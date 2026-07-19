using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Narratum.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniverses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UniverseId",
                table: "SaveSlots",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Universes",
                columns: table => new
                {
                    UniverseId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    GenreStyle = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    NarrativeStyle = table.Column<string>(type: "TEXT", nullable: true),
                    SerializedCharacters = table.Column<string>(type: "TEXT", nullable: true),
                    SerializedLocations = table.Column<string>(type: "TEXT", nullable: true),
                    OpeningAction = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultModel = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Universes", x => x.UniverseId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Universes");

            migrationBuilder.DropColumn(
                name: "UniverseId",
                table: "SaveSlots");
        }
    }
}
