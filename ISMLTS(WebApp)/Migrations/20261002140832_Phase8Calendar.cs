using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ISMLTS_WebApp_.Migrations
{
    /// <inheritdoc />
    public partial class Phase8Calendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OnDate",
                table: "TimetableSlots",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CalendarNotes",
                columns: table => new
                {
                    NoteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Role = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDone = table.Column<bool>(type: "bit", nullable: false),
                    Remind = table.Column<bool>(type: "bit", nullable: false),
                    RemindedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarNotes", x => x.NoteId);
                });

            migrationBuilder.CreateTable(
                name: "CollegeDates",
                columns: table => new
                {
                    CollegeDateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollegeDates", x => x.CollegeDateId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarNotes_Role_UserId_Date",
                table: "CalendarNotes",
                columns: new[] { "Role", "UserId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarNotes");

            migrationBuilder.DropTable(
                name: "CollegeDates");

            migrationBuilder.DropColumn(
                name: "OnDate",
                table: "TimetableSlots");
        }
    }
}
