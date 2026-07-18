using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Narratum.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PageSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SlotName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PageIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NarrativeText = table.Column<string>(type: "TEXT", nullable: true),
                    SerializedState = table.Column<string>(type: "TEXT", nullable: false),
                    IntentDescription = table.Column<string>(type: "TEXT", nullable: true),
                    ModelUsed = table.Column<string>(type: "TEXT", nullable: true),
                    GenreStyle = table.Column<string>(type: "TEXT", nullable: true),
                    SerializedPipelineResult = table.Column<string>(type: "TEXT", nullable: true),
                    PromptsSent = table.Column<string>(type: "TEXT", nullable: true),
                    RawLlmOutput = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SlotName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    SnapshotData = table.Column<string>(type: "TEXT", nullable: false),
                    SavedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SnapshotVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    IntegrityHash = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaveSlots",
                columns: table => new
                {
                    SlotName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    LastSavedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalEvents = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentChapterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaveSlots", x => x.SlotName);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PageSnapshots_SlotName_PageIndex",
                table: "PageSnapshots",
                columns: new[] { "SlotName", "PageIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedStates_SlotName",
                table: "SavedStates",
                column: "SlotName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageSnapshots");

            migrationBuilder.DropTable(
                name: "SavedStates");

            migrationBuilder.DropTable(
                name: "SaveSlots");
        }
    }
}
