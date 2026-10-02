using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ISMLTS_WebApp_.Migrations
{
    /// <inheritdoc />
    public partial class Phase8Terms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalendarTokenHash",
                table: "Students",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "AttendanceSessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Terms",
                columns: table => new
                {
                    TermId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terms", x => x.TermId);
                });

            migrationBuilder.CreateTable(
                name: "TimetableSlots",
                columns: table => new
                {
                    SlotId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModuleId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Venue = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetableSlots", x => x.SlotId);
                    table.ForeignKey(
                        name: "FK_TimetableSlots_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "ModuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Students_CalendarTokenHash",
                table: "Students",
                column: "CalendarTokenHash",
                unique: true,
                filter: "[CalendarTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableSlots_ModuleId",
                table: "TimetableSlots",
                column: "ModuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Terms");

            migrationBuilder.DropTable(
                name: "TimetableSlots");

            migrationBuilder.DropIndex(
                name: "IX_Students_CalendarTokenHash",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "CalendarTokenHash",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "AttendanceSessions");
        }
    }
}
