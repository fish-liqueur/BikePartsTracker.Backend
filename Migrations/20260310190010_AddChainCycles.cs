using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BikePartsTracker.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddChainCycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveChainId",
                table: "Bikes");

            migrationBuilder.DropColumn(
                name: "ChainCycleInterval",
                table: "Bikes");

            migrationBuilder.DropColumn(
                name: "ChainsCycleLength",
                table: "Bikes");

            migrationBuilder.DropColumn(
                name: "ChainsInCycleJson",
                table: "Bikes");

            migrationBuilder.CreateTable(
                name: "ChainCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BikeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChainsJson = table.Column<string>(type: "text", nullable: false),
                    ActiveChainId = table.Column<Guid>(type: "uuid", nullable: true),
                    IntervalKm = table.Column<double>(type: "double precision", nullable: true),
                    CycleLength = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChainCycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChainCycles_Bikes_BikeId",
                        column: x => x.BikeId,
                        principalTable: "Bikes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChainCycles_BikeId",
                table: "ChainCycles",
                column: "BikeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChainCycles");

            migrationBuilder.AddColumn<Guid>(
                name: "ActiveChainId",
                table: "Bikes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChainCycleInterval",
                table: "Bikes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChainsCycleLength",
                table: "Bikes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChainsInCycleJson",
                table: "Bikes",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
