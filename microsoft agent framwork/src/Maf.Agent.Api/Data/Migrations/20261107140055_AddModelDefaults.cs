using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maf.Agent.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddModelDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelDefaults",
                columns: table => new
                {
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModelName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelDefaults", x => x.Operation);
                    table.ForeignKey(
                        name: "FK_ModelDefaults_ModelProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "ModelProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelDefaults_ProviderId",
                table: "ModelDefaults",
                column: "ProviderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelDefaults");
        }
    }
}
