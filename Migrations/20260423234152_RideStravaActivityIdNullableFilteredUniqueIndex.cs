using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BikePartsTracker.Backend.Migrations
{
    /// <inheritdoc />
    public partial class RideStravaActivityIdNullableFilteredUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rides_UserId_StravaActivityId",
                table: "Rides");

            migrationBuilder.AlterColumn<long>(
                name: "StravaActivityId",
                table: "Rides",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateIndex(
                name: "IX_Rides_UserId_StravaActivityId",
                table: "Rides",
                columns: new[] { "UserId", "StravaActivityId" },
                unique: true,
                filter: "\"StravaActivityId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rides_UserId_StravaActivityId",
                table: "Rides");

            migrationBuilder.AlterColumn<long>(
                name: "StravaActivityId",
                table: "Rides",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rides_UserId_StravaActivityId",
                table: "Rides",
                columns: new[] { "UserId", "StravaActivityId" },
                unique: true);
        }
    }
}
