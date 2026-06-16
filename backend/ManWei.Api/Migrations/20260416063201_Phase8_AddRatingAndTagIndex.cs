using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManWei.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase8_AddRatingAndTagIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmotionTags_AnimeId",
                table: "EmotionTags");

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "Favorites",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Favorites_Rating",
                table: "Favorites",
                sql: "Rating IS NULL OR (Rating >= 1 AND Rating <= 10)");

            migrationBuilder.CreateIndex(
                name: "IX_EmotionTags_AnimeId_UserId_Name",
                table: "EmotionTags",
                columns: new[] { "AnimeId", "UserId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Favorites_Rating",
                table: "Favorites");

            migrationBuilder.DropIndex(
                name: "IX_EmotionTags_AnimeId_UserId_Name",
                table: "EmotionTags");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Favorites");

            migrationBuilder.CreateIndex(
                name: "IX_EmotionTags_AnimeId",
                table: "EmotionTags",
                column: "AnimeId");
        }
    }
}
