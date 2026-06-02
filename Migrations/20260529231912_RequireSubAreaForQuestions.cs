using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizGamePlatform.Migrations
{
    /// <inheritdoc />
    public partial class RequireSubAreaForQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, create a default "Uncategorized" Area and SubArea for orphaned questions
            migrationBuilder.Sql(@"
                INSERT INTO Areas (Name, Description) 
                SELECT 'Uncategorized', 'Default category for questions without a specific topic'
                WHERE NOT EXISTS (SELECT 1 FROM Areas WHERE Name = 'Uncategorized');
            ");

            migrationBuilder.Sql(@"
                INSERT INTO SubAreas (Name, AreaId) 
                SELECT 'General', (SELECT Id FROM Areas WHERE Name = 'Uncategorized' LIMIT 1)
                WHERE NOT EXISTS (SELECT 1 FROM SubAreas WHERE Name = 'General' AND AreaId = (SELECT Id FROM Areas WHERE Name = 'Uncategorized' LIMIT 1));
            ");

            // Update all questions with NULL SubAreaId to point to the default SubArea
            migrationBuilder.Sql(@"
                UPDATE Questions 
                SET SubAreaId = (SELECT Id FROM SubAreas WHERE Name = 'General' AND AreaId = (SELECT Id FROM Areas WHERE Name = 'Uncategorized' LIMIT 1) LIMIT 1)
                WHERE SubAreaId IS NULL;
            ");

            // Now make SubAreaId required
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_SubAreas_SubAreaId",
                table: "Questions");

            migrationBuilder.AlterColumn<int>(
                name: "SubAreaId",
                table: "Questions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_SubAreas_SubAreaId",
                table: "Questions",
                column: "SubAreaId",
                principalTable: "SubAreas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_SubAreas_SubAreaId",
                table: "Questions");

            migrationBuilder.AlterColumn<int>(
                name: "SubAreaId",
                table: "Questions",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_SubAreas_SubAreaId",
                table: "Questions",
                column: "SubAreaId",
                principalTable: "SubAreas",
                principalColumn: "Id");
        }
    }
}
