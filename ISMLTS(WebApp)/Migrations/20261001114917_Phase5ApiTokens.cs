using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ISMLTS_WebApp_.Migrations
{
    /// <inheritdoc />
    public partial class Phase5ApiTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiRefreshTokens",
                columns: table => new
                {
                    ApiRefreshTokenId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiRefreshTokens", x => x.ApiRefreshTokenId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiRefreshTokens_Role_UserId",
                table: "ApiRefreshTokens",
                columns: new[] { "Role", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiRefreshTokens_TokenHash",
                table: "ApiRefreshTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiRefreshTokens");
        }
    }
}
