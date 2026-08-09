using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Illumination.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentImportProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportProvenance",
                columns: table => new
                {
                    ImportBatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ImportedAt = table.Column<string>(type: "TEXT", nullable: false),
                    Contract = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: false),
                    ExternalBundleId = table.Column<string>(type: "TEXT", nullable: true),
                    GeneratedFor = table.Column<string>(type: "TEXT", nullable: true),
                    AcceptedOperationCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedLearningItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedLearningItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDeckCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedDeckCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignmentCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProvenance", x => x.ImportBatchId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportProvenance");
        }
    }
}
