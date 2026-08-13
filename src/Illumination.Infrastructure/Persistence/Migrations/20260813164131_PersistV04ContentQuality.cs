using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Illumination.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistV04ContentQuality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContentRevision",
                table: "LearningItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "QualityReviews",
                columns: table => new
                {
                    QualityReviewId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LearningItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", nullable: false),
                    EvidenceType = table.Column<string>(type: "TEXT", nullable: false),
                    Findings = table.Column<string>(type: "TEXT", nullable: false),
                    SuggestedCorrection = table.Column<string>(type: "TEXT", nullable: true),
                    SupersededBy = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityReviews", x => x.QualityReviewId);
                    table.ForeignKey(
                        name: "FK_QualityReviews_LearningItems_LearningItemId",
                        column: x => x.LearningItemId,
                        principalTable: "LearningItems",
                        principalColumn: "LearningItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QualityReviews_QualityReviews_SupersededBy",
                        column: x => x.SupersededBy,
                        principalTable: "QualityReviews",
                        principalColumn: "QualityReviewId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserFlagDefinitions",
                columns: table => new
                {
                    UserFlagDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Meaning = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFlagDefinitions", x => x.UserFlagDefinitionId);
                });

            migrationBuilder.CreateTable(
                name: "LearningItemUserFlags",
                columns: table => new
                {
                    LearningItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserFlagDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningItemUserFlags", x => new { x.LearningItemId, x.UserFlagDefinitionId });
                    table.ForeignKey(
                        name: "FK_LearningItemUserFlags_LearningItems_LearningItemId",
                        column: x => x.LearningItemId,
                        principalTable: "LearningItems",
                        principalColumn: "LearningItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearningItemUserFlags_UserFlagDefinitions_UserFlagDefinitionId",
                        column: x => x.UserFlagDefinitionId,
                        principalTable: "UserFlagDefinitions",
                        principalColumn: "UserFlagDefinitionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningItemUserFlags_UserFlagDefinitionId",
                table: "LearningItemUserFlags",
                column: "UserFlagDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityReviews_LearningItemId_ContentRevision",
                table: "QualityReviews",
                columns: new[] { "LearningItemId", "ContentRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_QualityReviews_SupersededBy",
                table: "QualityReviews",
                column: "SupersededBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningItemUserFlags");

            migrationBuilder.DropTable(
                name: "QualityReviews");

            migrationBuilder.DropTable(
                name: "UserFlagDefinitions");

            migrationBuilder.DropColumn(
                name: "ContentRevision",
                table: "LearningItems");
        }
    }
}
