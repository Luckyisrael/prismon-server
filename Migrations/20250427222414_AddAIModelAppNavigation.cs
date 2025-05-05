using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prismon.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAIModelAppNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIModels",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    AppId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    ExternalApiUrl = table.Column<string>(type: "TEXT", nullable: false),
                    ExternalApiKey = table.Column<string>(type: "TEXT", nullable: false),
                    InputType = table.Column<string>(type: "TEXT", nullable: false),
                    OutputType = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIModels_Apps_AppId",
                        column: x => x.AppId,
                        principalTable: "Apps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AIInvocations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ModelId = table.Column<string>(type: "TEXT", nullable: false),
                    InputType = table.Column<string>(type: "TEXT", nullable: false),
                    InputData = table.Column<string>(type: "TEXT", nullable: false),
                    Output = table.Column<string>(type: "TEXT", nullable: false),
                    Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    InvokedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIInvocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIInvocations_AIModels_ModelId",
                        column: x => x.ModelId,
                        principalTable: "AIModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIInvocations_ModelId",
                table: "AIInvocations",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_AIInvocations_UserId",
                table: "AIInvocations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AIModels_AppId",
                table: "AIModels",
                column: "AppId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIInvocations");

            migrationBuilder.DropTable(
                name: "AIModels");
        }
    }
}
