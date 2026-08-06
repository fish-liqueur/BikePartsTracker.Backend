using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BikePartsTracker.Backend.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// ADR 0002 Decision A: store all distances in metres (×1000 + rename *Km fields).
    /// Decision B: add nullable DistanceUnit preference column.
    /// StravaDistance is already metres and is intentionally left unchanged.
    /// </remarks>
    public partial class DistanceUnitsMetresAndPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Decision A — multiply former-km values before / after rename.
            migrationBuilder.Sql(
                """UPDATE "Bikes" SET "TotalDistance" = "TotalDistance" * 1000;""");

            migrationBuilder.RenameColumn(
                name: "IntervalKm",
                table: "ChainCycles",
                newName: "IntervalMetres");

            migrationBuilder.Sql(
                """UPDATE "ChainCycles" SET "IntervalMetres" = "IntervalMetres" * 1000 WHERE "IntervalMetres" IS NOT NULL;""");

            migrationBuilder.RenameColumn(
                name: "DefaultChainCycleIntervalKm",
                table: "UserSettings",
                newName: "DefaultChainCycleIntervalMetres");

            migrationBuilder.Sql(
                """UPDATE "UserSettings" SET "DefaultChainCycleIntervalMetres" = "DefaultChainCycleIntervalMetres" * 1000;""");

            // Decision B — explicit display preference (null = unset / infer on client).
            migrationBuilder.AddColumn<string>(
                name: "DistanceUnit",
                table: "UserSettings",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DistanceUnit",
                table: "UserSettings");

            migrationBuilder.Sql(
                """UPDATE "UserSettings" SET "DefaultChainCycleIntervalMetres" = "DefaultChainCycleIntervalMetres" / 1000;""");

            migrationBuilder.RenameColumn(
                name: "DefaultChainCycleIntervalMetres",
                table: "UserSettings",
                newName: "DefaultChainCycleIntervalKm");

            migrationBuilder.Sql(
                """UPDATE "ChainCycles" SET "IntervalMetres" = "IntervalMetres" / 1000 WHERE "IntervalMetres" IS NOT NULL;""");

            migrationBuilder.RenameColumn(
                name: "IntervalMetres",
                table: "ChainCycles",
                newName: "IntervalKm");

            migrationBuilder.Sql(
                """UPDATE "Bikes" SET "TotalDistance" = "TotalDistance" / 1000;""");
        }
    }
}
