using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BikePartsTracker.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSettingsBoolFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "defaultUseChainCycle",
                table: "UserSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "showTips",
                table: "UserSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "defaultUseChainCycle",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "showTips",
                table: "UserSettings");
        }
    }
}
