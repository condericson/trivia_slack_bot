using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace TriviaBot.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "BLOB", nullable: false),
                    DirectMessage = table.Column<string>(type: "TEXT", nullable: true),
                    PlayerMention = table.Column<string>(type: "TEXT", nullable: true),
                    PlayerName = table.Column<string>(type: "TEXT", nullable: true),
                    PlayerSlackId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.PlayerId);
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    QuestionId = table.Column<Guid>(type: "BLOB", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Statement = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.QuestionId);
                });

            migrationBuilder.CreateTable(
                name: "Answers",
                columns: table => new
                {
                    AnswerId = table.Column<Guid>(type: "BLOB", nullable: false),
                    IsCorrect = table.Column<bool>(type: "INTEGER", nullable: false),
                    QuestionId = table.Column<Guid>(type: "BLOB", nullable: true),
                    Statement = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Answers", x => x.AnswerId);
                    table.ForeignKey(
                        name: "FK_Answers_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "QuestionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attempts",
                columns: table => new
                {
                    AttemptId = table.Column<Guid>(type: "BLOB", nullable: false),
                    AnswerId = table.Column<Guid>(type: "BLOB", nullable: true),
                    Correct = table.Column<bool>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<Guid>(type: "BLOB", nullable: false),
                    QuestionId = table.Column<Guid>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attempts", x => x.AttemptId);
                    table.ForeignKey(
                        name: "FK_Attempts_Answers_AnswerId",
                        column: x => x.AnswerId,
                        principalTable: "Answers",
                        principalColumn: "AnswerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attempts_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Attempts_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "QuestionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Answers_QuestionId",
                table: "Answers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_AnswerId",
                table: "Attempts",
                column: "AnswerId");

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_PlayerId",
                table: "Attempts",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_QuestionId",
                table: "Attempts",
                column: "QuestionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attempts");

            migrationBuilder.DropTable(
                name: "Answers");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Questions");
        }
    }
}
