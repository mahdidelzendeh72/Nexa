using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Maf.Agent.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatTrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatTraceThreads",
                columns: table => new
                {
                    ThreadId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RunCount = table.Column<int>(type: "integer", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastOutcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TotalTokens = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatTraceThreads", x => x.ThreadId);
                });

            migrationBuilder.CreateTable(
                name: "ChatTraceRuns",
                columns: table => new
                {
                    RunId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ThreadId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    ReasoningTokens = table.Column<long>(type: "bigint", nullable: true),
                    TotalTokens = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatTraceRuns", x => x.RunId);
                    table.ForeignKey(
                        name: "FK_ChatTraceRuns_ChatTraceThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "ChatTraceThreads",
                        principalColumn: "ThreadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatTraceSteps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "character varying(80)", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RefId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Size = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatTraceSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatTraceSteps_ChatTraceRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "ChatTraceRuns",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatTraceRuns_ThreadId_StartedAt",
                table: "ChatTraceRuns",
                columns: new[] { "ThreadId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatTraceSteps_RunId_Sequence",
                table: "ChatTraceSteps",
                columns: new[] { "RunId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatTraceThreads_UserId_LastRunAt",
                table: "ChatTraceThreads",
                columns: new[] { "UserId", "LastRunAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatTraceSteps");

            migrationBuilder.DropTable(
                name: "ChatTraceRuns");

            migrationBuilder.DropTable(
                name: "ChatTraceThreads");
        }
    }
}
