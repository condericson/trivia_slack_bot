using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace TriviaBot.Migrations
{
    public partial class removeddirectmessages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attempts_Questions_QuestionId",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "DirectMessage",
                table: "Players");

            migrationBuilder.AlterColumn<Guid>(
                name: "QuestionId",
                table: "Attempts",
                type: "BLOB",
                nullable: true,
                oldClrType: typeof(Guid));

            migrationBuilder.AddForeignKey(
                name: "FK_Attempts_Questions_QuestionId",
                table: "Attempts",
                column: "QuestionId",
                principalTable: "Questions",
                principalColumn: "QuestionId",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attempts_Questions_QuestionId",
                table: "Attempts");

            migrationBuilder.AddColumn<string>(
                name: "DirectMessage",
                table: "Players",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "QuestionId",
                table: "Attempts",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "BLOB",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Attempts_Questions_QuestionId",
                table: "Attempts",
                column: "QuestionId",
                principalTable: "Questions",
                principalColumn: "QuestionId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
