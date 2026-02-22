using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BikePartsTracker.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddBikeAndBikePartFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BikeParts_Bikes_BikeId",
                table: "BikeParts");

            migrationBuilder.AlterColumn<int>(
                name: "DefaultChainCycleLength",
                table: "UserSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DefaultChainCycleIntervalKm",
                table: "UserSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

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
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Bikes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Bikes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<Guid>(
                name: "BikeId",
                table: "BikeParts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "BikeParts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "BikeParts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InstallationDate",
                table: "BikeParts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MileageAtInstallation",
                table: "BikeParts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "BikeParts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PartType",
                table: "BikeParts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "BikeParts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddForeignKey(
                name: "FK_BikeParts_Bikes_BikeId",
                table: "BikeParts",
                column: "BikeId",
                principalTable: "Bikes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BikeParts_Bikes_BikeId",
                table: "BikeParts");

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

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Bikes");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Bikes");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "BikeParts");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "BikeParts");

            migrationBuilder.DropColumn(
                name: "InstallationDate",
                table: "BikeParts");

            migrationBuilder.DropColumn(
                name: "MileageAtInstallation",
                table: "BikeParts");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "BikeParts");

            migrationBuilder.DropColumn(
                name: "PartType",
                table: "BikeParts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "BikeParts");

            migrationBuilder.AlterColumn<int>(
                name: "DefaultChainCycleLength",
                table: "UserSettings",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "DefaultChainCycleIntervalKm",
                table: "UserSettings",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "BikeId",
                table: "BikeParts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BikeParts_Bikes_BikeId",
                table: "BikeParts",
                column: "BikeId",
                principalTable: "Bikes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
