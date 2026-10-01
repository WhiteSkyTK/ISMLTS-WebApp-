using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ISMLTS_WebApp_.Migrations
{
    /// <inheritdoc />
    public partial class Phase3Marking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssessmentId",
                table: "Marks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Feedback",
                table: "Marks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MarksReleased",
                table: "Assessments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MarksReleasedAt",
                table: "Assessments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxScore",
                table: "Assessments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 100m);

            migrationBuilder.CreateTable(
                name: "MarkChanges",
                columns: table => new
                {
                    MarkChangeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MarkId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    ModuleId = table.Column<int>(type: "int", nullable: false),
                    AssessmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OldScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NewScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OldMaxScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NewMaxScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FeedbackChanged = table.Column<bool>(type: "bit", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChangedById = table.Column<int>(type: "int", nullable: false),
                    ChangedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkChanges", x => x.MarkChangeId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Marks_AssessmentId",
                table: "Marks",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkChanges_MarkId",
                table: "MarkChanges",
                column: "MarkId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkChanges_ModuleId",
                table: "MarkChanges",
                column: "ModuleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Marks_Assessments_AssessmentId",
                table: "Marks",
                column: "AssessmentId",
                principalTable: "Assessments",
                principalColumn: "AssessmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Marks_Assessments_AssessmentId",
                table: "Marks");

            migrationBuilder.DropTable(
                name: "MarkChanges");

            migrationBuilder.DropIndex(
                name: "IX_Marks_AssessmentId",
                table: "Marks");

            migrationBuilder.DropColumn(
                name: "AssessmentId",
                table: "Marks");

            migrationBuilder.DropColumn(
                name: "Feedback",
                table: "Marks");

            migrationBuilder.DropColumn(
                name: "MarksReleased",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "MarksReleasedAt",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "MaxScore",
                table: "Assessments");
        }
    }
}
