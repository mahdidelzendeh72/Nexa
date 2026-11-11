using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maf.Agent.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatTurnUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatTurnUsages",
                columns: table => new
                {
                    EntryId = table.Column<long>(type: "bigint", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    ReasoningTokens = table.Column<long>(type: "bigint", nullable: true),
                    CachedInputTokens = table.Column<long>(type: "bigint", nullable: true),
                    TotalTokens = table.Column<long>(type: "bigint", nullable: true),
                    CacheWriteInputTokens = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatTurnUsages", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_ChatTurnUsages_ChatEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "ChatEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatTurnUsages");
        }
    }
}
