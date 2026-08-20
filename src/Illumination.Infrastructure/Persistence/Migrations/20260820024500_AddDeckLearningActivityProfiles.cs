using Illumination.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Illumination.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IlluminationDbContext))]
[Migration("20260820024500_AddDeckLearningActivityProfiles")]
public partial class AddDeckLearningActivityProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DeckLearningActivityProfiles",
            columns: table => new
            {
                DeckId = table.Column<Guid>(type: "TEXT", nullable: false),
                Profile = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeckLearningActivityProfiles", x => new { x.DeckId, x.Profile });
                table.ForeignKey(
                    name: "FK_DeckLearningActivityProfiles_Decks_DeckId",
                    column: x => x.DeckId,
                    principalTable: "Decks",
                    principalColumn: "DeckId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DeckLearningActivityProfiles_Profile",
            table: "DeckLearningActivityProfiles",
            column: "Profile");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DeckLearningActivityProfiles");
    }
}
