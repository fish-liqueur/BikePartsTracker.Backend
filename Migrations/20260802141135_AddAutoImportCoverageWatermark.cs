using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BikePartsTracker.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoImportCoverageWatermark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AutoImportCoveredFrom",
                table: "ExternalServiceIntegrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AutoImportCoveredTo",
                table: "ExternalServiceIntegrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalServiceIntegrations_ServiceType_ServiceUserId",
                table: "ExternalServiceIntegrations",
                columns: new[] { "ServiceType", "ServiceUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExternalServiceIntegrations_ServiceType_ServiceUserId",
                table: "ExternalServiceIntegrations");

            migrationBuilder.DropColumn(
                name: "AutoImportCoveredFrom",
                table: "ExternalServiceIntegrations");

            migrationBuilder.DropColumn(
                name: "AutoImportCoveredTo",
                table: "ExternalServiceIntegrations");
        }
    }
}
