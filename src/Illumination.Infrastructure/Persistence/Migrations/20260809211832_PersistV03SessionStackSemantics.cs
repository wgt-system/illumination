using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Illumination.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistV03SessionStackSemantics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InterveningCardTarget",
                table: "LearningItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InterveningCardTarget",
                table: "LearningItems",
                type: "INTEGER",
                nullable: true);
        }
    }
}
