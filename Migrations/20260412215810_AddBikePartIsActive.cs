using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BikePartsTracker.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddBikePartIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "BikeParts",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "BikeParts");
        }
    }
}
