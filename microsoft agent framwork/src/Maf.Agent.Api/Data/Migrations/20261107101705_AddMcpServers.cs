using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Maf.Agent.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpServers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "McpServers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Transport = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    HeadersJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Command = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    ArgumentsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    EnvironmentJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastDiscoveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DiscoveryError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "McpDiscoveredTools",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpDiscoveredTools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpDiscoveredTools_McpServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "McpServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_McpDiscoveredTools_ServerId_Name",
                table: "McpDiscoveredTools",
                columns: new[] { "ServerId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpServers_IsEnabled",
                table: "McpServers",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_McpServers_NormalizedName",
                table: "McpServers",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "McpDiscoveredTools");

            migrationBuilder.DropTable(
                name: "McpServers");
        }
    }
}
