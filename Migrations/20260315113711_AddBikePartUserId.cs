using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BikePartsTracker.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddBikePartUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add as nullable first so existing rows don't violate the FK immediately
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "BikeParts",
                type: "uuid",
                nullable: true);

            // Backfill UserId from the owning bike for parts that have a BikeId
            migrationBuilder.Sql(@"
                UPDATE ""BikeParts"" bp
                SET ""UserId"" = b.""UserId""
                FROM ""Bikes"" b
                WHERE bp.""BikeId"" = b.""Id""
                  AND bp.""UserId"" IS NULL;
            ");

            // Delete orphaned parts (no bike → no owner to derive from)
            migrationBuilder.Sql(@"
                DELETE FROM ""BikeParts"" WHERE ""UserId"" IS NULL;
            ");

            // Now tighten to NOT NULL
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "BikeParts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BikeParts_UserId",
                table: "BikeParts",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BikeParts_Users_UserId",
                table: "BikeParts",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BikeParts_Users_UserId",
                table: "BikeParts");

            migrationBuilder.DropIndex(
                name: "IX_BikeParts_UserId",
                table: "BikeParts");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "BikeParts");
        }
    }
}
