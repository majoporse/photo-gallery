using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gallery.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetireProcessingStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No schema change: `Processing` stops existing as a status. A row
            // still in it at deploy time would fail to parse, and it is exactly
            // the kind of row the sweeper exists to rescue.
            migrationBuilder.Sql(
                """UPDATE "Uploads" SET "Status" = 'Queued' WHERE "Status" = 'Processing';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Which rows were really Processing is not recoverable, and Queued is
            // the state that keeps them moving.
        }
    }
}
