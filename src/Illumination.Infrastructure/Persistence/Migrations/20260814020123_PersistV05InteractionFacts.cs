using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Illumination.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistV05InteractionFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ConsiderAssistance",
                table: "StudySessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationMode",
                table: "StudySessions",
                type: "TEXT",
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<bool>(
                name: "LowInteractionOnly",
                table: "StudySessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AssistanceAnswerChoicesRevealed",
                table: "Reviews",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutomaticCorrectness",
                table: "Reviews",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HintCount",
                table: "Reviews",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ReferenceSolutionRevealed",
                table: "Reviews",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedAssessment",
                table: "Reviews",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChoiceId",
                table: "AnswerChoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("UPDATE AnswerChoices SET ChoiceId = CASE WHEN Role = 'Direct' THEN 'legacy-direct-' ELSE 'legacy-assistance-' END || Position WHERE ChoiceId IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_AnswerChoices_LearningItemId_Role_ChoiceId",
                table: "AnswerChoices",
                columns: new[] { "LearningItemId", "Role", "ChoiceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnswerChoices_LearningItemId_Role_ChoiceId",
                table: "AnswerChoices");

            migrationBuilder.DropColumn(
                name: "ConsiderAssistance",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "EvaluationMode",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "LowInteractionOnly",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "AssistanceAnswerChoicesRevealed",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "AutomaticCorrectness",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "HintCount",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ReferenceSolutionRevealed",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "SuggestedAssessment",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ChoiceId",
                table: "AnswerChoices");
        }
    }
}
