using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BikePartsTracker.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddRidesWorksUsagePeriodsAndRideIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PartUsageHistories_BikePartId",
                table: "PartUsageHistories");

            migrationBuilder.DropColumn(
                name: "ActionType",
                table: "PartUsageHistories");

            migrationBuilder.RenameColumn(
                name: "OdometerAtAction",
                table: "PartUsageHistories",
                newName: "Distance");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "PartUsageHistories",
                newName: "UpdatedAt");

            migrationBuilder.AddColumn<Guid>(
                name: "BikeId",
                table: "PartUsageHistories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "PartUsageHistories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "PartUsageHistories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsShadow",
                table: "PartUsageHistories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceUsagePeriodId",
                table: "PartUsageHistories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "PartUsageHistories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "WorkId",
                table: "PartUsageHistories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Rides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StravaActivityId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BikeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    GearId = table.Column<string>(type: "text", nullable: true),
                    Distance = table.Column<double>(type: "double precision", nullable: false),
                    UserDistance = table.Column<double>(type: "double precision", nullable: false),
                    StartDateLocal = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rides_Bikes_BikeId",
                        column: x => x.BikeId,
                        principalTable: "Bikes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Rides_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Works",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    TriggerType = table.Column<int>(type: "integer", nullable: false),
                    ParentType = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerValue = table.Column<double>(type: "double precision", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Works", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Works_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartUsageHistories_BikeId",
                table: "PartUsageHistories",
                column: "BikeId");

            migrationBuilder.CreateIndex(
                name: "IX_PartUsageHistories_BikePartId_StartDate_EndDate",
                table: "PartUsageHistories",
                columns: new[] { "BikePartId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PartUsageHistories_SourceUsagePeriodId",
                table: "PartUsageHistories",
                column: "SourceUsagePeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_PartUsageHistories_WorkId",
                table: "PartUsageHistories",
                column: "WorkId");

            migrationBuilder.CreateIndex(
                name: "IX_Rides_BikeId",
                table: "Rides",
                column: "BikeId");

            migrationBuilder.CreateIndex(
                name: "IX_Rides_UserId_StartDateLocal",
                table: "Rides",
                columns: new[] { "UserId", "StartDateLocal" });

            migrationBuilder.CreateIndex(
                name: "IX_Rides_UserId_StravaActivityId",
                table: "Rides",
                columns: new[] { "UserId", "StravaActivityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Works_UserId_ParentType_ParentId_IsActive",
                table: "Works",
                columns: new[] { "UserId", "ParentType", "ParentId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_PartUsageHistories_Bikes_BikeId",
                table: "PartUsageHistories",
                column: "BikeId",
                principalTable: "Bikes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PartUsageHistories_PartUsageHistories_SourceUsagePeriodId",
                table: "PartUsageHistories",
                column: "SourceUsagePeriodId",
                principalTable: "PartUsageHistories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PartUsageHistories_Works_WorkId",
                table: "PartUsageHistories",
                column: "WorkId",
                principalTable: "Works",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartUsageHistories_Bikes_BikeId",
                table: "PartUsageHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_PartUsageHistories_PartUsageHistories_SourceUsagePeriodId",
                table: "PartUsageHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_PartUsageHistories_Works_WorkId",
                table: "PartUsageHistories");

            migrationBuilder.DropTable(
                name: "Rides");

            migrationBuilder.DropTable(
                name: "Works");

            migrationBuilder.DropIndex(
                name: "IX_PartUsageHistories_BikeId",
                table: "PartUsageHistories");

            migrationBuilder.DropIndex(
                name: "IX_PartUsageHistories_BikePartId_StartDate_EndDate",
                table: "PartUsageHistories");

            migrationBuilder.DropIndex(
                name: "IX_PartUsageHistories_SourceUsagePeriodId",
                table: "PartUsageHistories");

            migrationBuilder.DropIndex(
                name: "IX_PartUsageHistories_WorkId",
                table: "PartUsageHistories");

            migrationBuilder.DropColumn(
                name: "BikeId",
                table: "PartUsageHistories");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PartUsageHistories");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "PartUsageHistories");

            migrationBuilder.DropColumn(
                name: "IsShadow",
                table: "PartUsageHistories");

            migrationBuilder.DropColumn(
                name: "SourceUsagePeriodId",
                table: "PartUsageHistories");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "PartUsageHistories");

            migrationBuilder.DropColumn(
                name: "WorkId",
                table: "PartUsageHistories");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "PartUsageHistories",
                newName: "Date");

            migrationBuilder.RenameColumn(
                name: "Distance",
                table: "PartUsageHistories",
                newName: "OdometerAtAction");

            migrationBuilder.AddColumn<string>(
                name: "ActionType",
                table: "PartUsageHistories",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PartUsageHistories_BikePartId",
                table: "PartUsageHistories",
                column: "BikePartId");
        }
    }
}
