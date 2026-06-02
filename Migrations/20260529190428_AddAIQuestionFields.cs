using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizGamePlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddAIQuestionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAIGenerated",
                table: "Questions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Questions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "QuestionEmbeddings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    EmbeddingJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionEmbeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionEmbeddings_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionEmbeddings_QuestionId",
                table: "QuestionEmbeddings",
                column: "QuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionEmbeddings");

            migrationBuilder.DropColumn(
                name: "IsAIGenerated",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Questions");
        }
    }
}
