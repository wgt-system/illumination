using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Illumination.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IlluminationDbContext))]
[Migration("20260820013000_AddDeckTopicLabels")]
public partial class AddDeckTopicLabels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DeckTopicLabels",
            columns: table => new
            {
                DeckId = table.Column<Guid>(type: "TEXT", nullable: false),
                Label = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false, collation: "NOCASE")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeckTopicLabels", x => new { x.DeckId, x.Label });
                table.ForeignKey(
                    name: "FK_DeckTopicLabels_Decks_DeckId",
                    column: x => x.DeckId,
                    principalTable: "Decks",
                    principalColumn: "DeckId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DeckTopicLabels_Label",
            table: "DeckTopicLabels",
            column: "Label");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DeckTopicLabels");
    }
}
