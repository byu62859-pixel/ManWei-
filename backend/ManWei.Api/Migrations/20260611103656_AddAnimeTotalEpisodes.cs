using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManWei.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimeTotalEpisodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TotalEpisodes",
                table: "Anime",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalEpisodes",
                table: "Anime");
        }
    }
}
