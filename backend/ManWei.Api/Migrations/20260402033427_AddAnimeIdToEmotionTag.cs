using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManWei.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimeIdToEmotionTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmotionTags_UserId",
                table: "EmotionTags");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "EmotionTags",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "AnimeId",
                table: "EmotionTags",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmotionTags_AnimeId",
                table: "EmotionTags",
                column: "AnimeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmotionTags_UserId_AnimeId_Name",
                table: "EmotionTags",
                columns: new[] { "UserId", "AnimeId", "Name" },
                unique: true,
                filter: "[UserId] IS NOT NULL AND [AnimeId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_EmotionTags_Anime_AnimeId",
                table: "EmotionTags",
                column: "AnimeId",
                principalTable: "Anime",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmotionTags_Anime_AnimeId",
                table: "EmotionTags");

            migrationBuilder.DropIndex(
                name: "IX_EmotionTags_AnimeId",
                table: "EmotionTags");

            migrationBuilder.DropIndex(
                name: "IX_EmotionTags_UserId_AnimeId_Name",
                table: "EmotionTags");

            migrationBuilder.DropColumn(
                name: "AnimeId",
                table: "EmotionTags");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "EmotionTags",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_EmotionTags_UserId",
                table: "EmotionTags",
                column: "UserId");
        }
    }
}
