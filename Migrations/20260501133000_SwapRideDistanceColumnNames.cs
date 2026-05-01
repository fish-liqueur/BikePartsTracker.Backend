using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BikePartsTracker.Backend.Migrations
{
    /// <inheritdoc />
    public partial class SwapRideDistanceColumnNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "Distance" currently holds raw Strava data; "RecordedDistance" holds business/user data.
            // Swap so that "Distance" = business and "RecordedDistance" = raw Strava.
            migrationBuilder.RenameColumn(
                name: "Distance",
                table: "Rides",
                newName: "TempDist");

            migrationBuilder.RenameColumn(
                name: "RecordedDistance",
                table: "Rides",
                newName: "Distance");

            migrationBuilder.RenameColumn(
                name: "TempDist",
                table: "Rides",
                newName: "RecordedDistance");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Distance",
                table: "Rides",
                newName: "TempDist");

            migrationBuilder.RenameColumn(
                name: "RecordedDistance",
                table: "Rides",
                newName: "Distance");

            migrationBuilder.RenameColumn(
                name: "TempDist",
                table: "Rides",
                newName: "RecordedDistance");
        }
    }
}
