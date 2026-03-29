using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BikePartsTracker.Backend.Migrations
{
    /// <inheritdoc />
    public partial class SeparateChainCyclesFromBike : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChainsJson",
                table: "ChainCycles");

            migrationBuilder.DropColumn(
                name: "CycleLength",
                table: "ChainCycles");

            migrationBuilder.AddColumn<Guid>(
                name: "ChainCycleId",
                table: "BikeParts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChainCyclePosition",
                table: "BikeParts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BikeParts_ChainCycleId",
                table: "BikeParts",
                column: "ChainCycleId");

            migrationBuilder.AddForeignKey(
                name: "FK_BikeParts_ChainCycles_ChainCycleId",
                table: "BikeParts",
                column: "ChainCycleId",
                principalTable: "ChainCycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BikeParts_ChainCycles_ChainCycleId",
                table: "BikeParts");

            migrationBuilder.DropIndex(
                name: "IX_BikeParts_ChainCycleId",
                table: "BikeParts");

            migrationBuilder.DropColumn(
                name: "ChainCycleId",
                table: "BikeParts");

            migrationBuilder.DropColumn(
                name: "ChainCyclePosition",
                table: "BikeParts");

            migrationBuilder.AddColumn<string>(
                name: "ChainsJson",
                table: "ChainCycles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CycleLength",
                table: "ChainCycles",
                type: "integer",
                nullable: true);
        }
    }
}
