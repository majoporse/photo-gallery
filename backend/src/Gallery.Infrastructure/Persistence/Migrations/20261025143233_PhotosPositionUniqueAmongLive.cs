using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gallery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhotosPositionUniqueAmongLive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Photos_CollectionId_Position",
                table: "Photos");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_CollectionId_Position",
                table: "Photos",
                columns: new[] { "CollectionId", "Position" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Photos_CollectionId_Position",
                table: "Photos");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_CollectionId_Position",
                table: "Photos",
                columns: new[] { "CollectionId", "Position" },
                unique: true);
        }
    }
}
