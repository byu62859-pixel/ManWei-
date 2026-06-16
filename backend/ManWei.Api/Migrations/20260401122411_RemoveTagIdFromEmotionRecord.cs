using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManWei.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTagIdFromEmotionRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmotionRecords_EmotionTags_TagId",
                table: "EmotionRecords");

            migrationBuilder.DropIndex(
                name: "IX_EmotionRecords_FavoriteId_Episode",
                table: "EmotionRecords");

            migrationBuilder.DropIndex(
                name: "IX_EmotionRecords_TagId",
                table: "EmotionRecords");

            migrationBuilder.DropColumn(
                name: "TagId",
                table: "EmotionRecords");

            migrationBuilder.AlterColumn<int>(
                name: "Episode",
                table: "EmotionRecords",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmotionTagId",
                table: "EmotionRecords",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmotionRecords_EmotionTagId",
                table: "EmotionRecords",
                column: "EmotionTagId");

            migrationBuilder.CreateIndex(
                name: "IX_EmotionRecords_FavoriteId_Episode",
                table: "EmotionRecords",
                columns: new[] { "FavoriteId", "Episode" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EmotionRecords_EmotionTags_EmotionTagId",
                table: "EmotionRecords",
                column: "EmotionTagId",
                principalTable: "EmotionTags",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmotionRecords_EmotionTags_EmotionTagId",
                table: "EmotionRecords");

            migrationBuilder.DropIndex(
                name: "IX_EmotionRecords_EmotionTagId",
                table: "EmotionRecords");

            migrationBuilder.DropIndex(
                name: "IX_EmotionRecords_FavoriteId_Episode",
                table: "EmotionRecords");

            migrationBuilder.DropColumn(
                name: "EmotionTagId",
                table: "EmotionRecords");

            migrationBuilder.AlterColumn<int>(
                name: "Episode",
                table: "EmotionRecords",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "TagId",
                table: "EmotionRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_EmotionRecords_FavoriteId_Episode",
                table: "EmotionRecords",
                columns: new[] { "FavoriteId", "Episode" },
                unique: true,
                filter: "[Episode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmotionRecords_TagId",
                table: "EmotionRecords",
                column: "TagId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmotionRecords_EmotionTags_TagId",
                table: "EmotionRecords",
                column: "TagId",
                principalTable: "EmotionTags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
