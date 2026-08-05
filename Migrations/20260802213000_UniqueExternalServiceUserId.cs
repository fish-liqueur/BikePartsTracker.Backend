using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BikePartsTracker.Backend.Migrations
{
    /// <inheritdoc />
    public partial class UniqueExternalServiceUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExternalServiceIntegrations_ServiceType_ServiceUserId",
                table: "ExternalServiceIntegrations");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalServiceIntegrations_ServiceType_ServiceUserId",
                table: "ExternalServiceIntegrations",
                columns: new[] { "ServiceType", "ServiceUserId" },
                unique: true,
                filter: "\"ServiceUserId\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExternalServiceIntegrations_ServiceType_ServiceUserId",
                table: "ExternalServiceIntegrations");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalServiceIntegrations_ServiceType_ServiceUserId",
                table: "ExternalServiceIntegrations",
                columns: new[] { "ServiceType", "ServiceUserId" });
        }
    }
}
