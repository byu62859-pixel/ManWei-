using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManWei.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEmotionTagIdFromEmotionRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmotionRecords_EmotionTags_EmotionTagId",
                table: "EmotionRecords");

            migrationBuilder.DropIndex(
                name: "IX_EmotionRecords_EmotionTagId",
                table: "EmotionRecords");

            migrationBuilder.DropColumn(
                name: "EmotionTagId",
                table: "EmotionRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmotionTagId",
                table: "EmotionRecords",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmotionRecords_EmotionTagId",
                table: "EmotionRecords",
                column: "EmotionTagId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmotionRecords_EmotionTags_EmotionTagId",
                table: "EmotionRecords",
                column: "EmotionTagId",
                principalTable: "EmotionTags",
                principalColumn: "Id");
        }
    }
}
