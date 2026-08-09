using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Illumination.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistV02StudyAndReviewState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Difficulty",
                table: "LearningItems",
                type: "REAL",
                nullable: false,
                defaultValue: 5.0);

            migrationBuilder.AddColumn<int>(
                name: "InterveningCardTarget",
                table: "LearningItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInShortTermRelearning",
                table: "LearningItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "StabilityDays",
                table: "LearningItems",
                type: "REAL",
                nullable: false,
                defaultValue: 0.5);

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    ReviewId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LearningItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<string>(type: "TEXT", nullable: false),
                    Assessment = table.Column<string>(type: "TEXT", nullable: false),
                    SubmittedResponse = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.ReviewId);
                    table.ForeignKey(
                        name: "FK_Reviews_LearningItems_LearningItemId",
                        column: x => x.LearningItemId,
                        principalTable: "LearningItems",
                        principalColumn: "LearningItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudySessions",
                columns: table => new
                {
                    StudySessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<string>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudySessions", x => x.StudySessionId);
                });

            migrationBuilder.CreateTable(
                name: "StudySessionDecks",
                columns: table => new
                {
                    StudySessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeckId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudySessionDecks", x => new { x.StudySessionId, x.DeckId });
                    table.ForeignKey(
                        name: "FK_StudySessionDecks_StudySessions_StudySessionId",
                        column: x => x.StudySessionId,
                        principalTable: "StudySessions",
                        principalColumn: "StudySessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudySessionQueue",
                columns: table => new
                {
                    StudySessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    LearningItemId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudySessionQueue", x => new { x.StudySessionId, x.Position });
                    table.ForeignKey(
                        name: "FK_StudySessionQueue_StudySessions_StudySessionId",
                        column: x => x.StudySessionId,
                        principalTable: "StudySessions",
                        principalColumn: "StudySessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudySessionReviews",
                columns: table => new
                {
                    StudySessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudySessionReviews", x => new { x.StudySessionId, x.Position });
                    table.ForeignKey(
                        name: "FK_StudySessionReviews_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "ReviewId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudySessionReviews_StudySessions_StudySessionId",
                        column: x => x.StudySessionId,
                        principalTable: "StudySessions",
                        principalColumn: "StudySessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_LearningItemId",
                table: "Reviews",
                column: "LearningItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StudySessionReviews_ReviewId",
                table: "StudySessionReviews",
                column: "ReviewId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudySessionDecks");

            migrationBuilder.DropTable(
                name: "StudySessionQueue");

            migrationBuilder.DropTable(
                name: "StudySessionReviews");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "StudySessions");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "LearningItems");

            migrationBuilder.DropColumn(
                name: "InterveningCardTarget",
                table: "LearningItems");

            migrationBuilder.DropColumn(
                name: "IsInShortTermRelearning",
                table: "LearningItems");

            migrationBuilder.DropColumn(
                name: "StabilityDays",
                table: "LearningItems");
        }
    }
}
