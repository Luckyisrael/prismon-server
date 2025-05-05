using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prismon.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddApiUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomRateLimit",
                table: "Apps",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tier",
                table: "Apps",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomRateLimit",
                table: "Apps");

            migrationBuilder.DropColumn(
                name: "Tier",
                table: "Apps");
        }
    }
}
