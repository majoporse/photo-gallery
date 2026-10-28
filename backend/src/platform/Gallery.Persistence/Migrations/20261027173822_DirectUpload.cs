using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gallery.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DirectUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NextPhotoPosition",
                table: "Collections",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing collections must not hand out a position that a photo
            // already occupies. The counter only grows from here.
            migrationBuilder.Sql(
                """
                UPDATE "Collections" AS c
                SET "NextPhotoPosition" = COALESCE((
                    SELECT MAX(p."Position") + 1
                    FROM "Photos" AS p
                    WHERE p."CollectionId" = c."Id"
                ), 0);
                """);

            migrationBuilder.CreateTable(
                name: "Uploads",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    CollectionId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    PhotoId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    DeclaredBytes = table.Column<long>(type: "bigint", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Rejection = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Failure = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uploads", x => x.Id);
                    table.CheckConstraint("CK_Uploads_Bytes", "\"DeclaredBytes\" > 0");
                    table.CheckConstraint("CK_Uploads_Position", "\"Position\" >= 0");
                    table.ForeignKey(
                        name: "FK_Uploads_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_CollectionId",
                table: "Uploads",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_Status_UpdatedAt",
                table: "Uploads",
                columns: new[] { "Status", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Uploads");

            migrationBuilder.DropColumn(
                name: "NextPhotoPosition",
                table: "Collections");
        }
    }
}
