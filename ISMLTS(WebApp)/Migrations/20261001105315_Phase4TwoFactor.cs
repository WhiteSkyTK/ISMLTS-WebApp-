using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ISMLTS_WebApp_.Migrations
{
    /// <inheritdoc />
    public partial class Phase4TwoFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Students",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TwoFactorEnabled",
                table: "Students",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "TwoFactorLastStep",
                table: "Students",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "TwoFactorRecoveryCodes",
                table: "Students",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwoFactorSecret",
                table: "Students",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Lecturers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TwoFactorEnabled",
                table: "Lecturers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "TwoFactorLastStep",
                table: "Lecturers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "TwoFactorRecoveryCodes",
                table: "Lecturers",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwoFactorSecret",
                table: "Lecturers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Admins",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TwoFactorEnabled",
                table: "Admins",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "TwoFactorLastStep",
                table: "Admins",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "TwoFactorRecoveryCodes",
                table: "Admins",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwoFactorSecret",
                table: "Admins",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "TwoFactorEnabled",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "TwoFactorLastStep",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "TwoFactorRecoveryCodes",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "TwoFactorSecret",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Lecturers");

            migrationBuilder.DropColumn(
                name: "TwoFactorEnabled",
                table: "Lecturers");

            migrationBuilder.DropColumn(
                name: "TwoFactorLastStep",
                table: "Lecturers");

            migrationBuilder.DropColumn(
                name: "TwoFactorRecoveryCodes",
                table: "Lecturers");

            migrationBuilder.DropColumn(
                name: "TwoFactorSecret",
                table: "Lecturers");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "TwoFactorEnabled",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "TwoFactorLastStep",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "TwoFactorRecoveryCodes",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "TwoFactorSecret",
                table: "Admins");
        }
    }
}
