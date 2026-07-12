using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BikePartsTracker.Backend.Migrations
{
    /// <inheritdoc />
    public partial class RenameWorkToMaintenanceTask : Migration
    {
        // This migration renames the "Works" concept to "MaintenanceTask" at the database level.
        // It is written as in-place renames (table, columns, indexes, and constraints) rather than
        // the default drop/create EF scaffolds, so existing rows are preserved and the DB names
        // match the model names.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename the table.
            migrationBuilder.RenameTable(
                name: "Works",
                newName: "MaintenanceTasks");

            // Rename the primary key and foreign key constraints to follow the new convention.
            migrationBuilder.Sql("ALTER TABLE \"MaintenanceTasks\" RENAME CONSTRAINT \"PK_Works\" TO \"PK_MaintenanceTasks\";");
            migrationBuilder.Sql("ALTER TABLE \"MaintenanceTasks\" RENAME CONSTRAINT \"FK_Works_Users_UserId\" TO \"FK_MaintenanceTasks_Users_UserId\";");

            // Rename the index on the renamed table.
            migrationBuilder.RenameIndex(
                name: "IX_Works_UserId_ParentType_ParentId_IsActive",
                table: "MaintenanceTasks",
                newName: "IX_MaintenanceTasks_UserId_ParentType_ParentId_IsActive");

            // Rename the foreign key column on PartUsageHistories and its supporting index/constraint.
            migrationBuilder.RenameColumn(
                name: "WorkId",
                table: "PartUsageHistories",
                newName: "MaintenanceTaskId");

            migrationBuilder.RenameIndex(
                name: "IX_PartUsageHistories_WorkId",
                table: "PartUsageHistories",
                newName: "IX_PartUsageHistories_MaintenanceTaskId");

            migrationBuilder.Sql("ALTER TABLE \"PartUsageHistories\" RENAME CONSTRAINT \"FK_PartUsageHistories_Works_WorkId\" TO \"FK_PartUsageHistories_MaintenanceTasks_MaintenanceTaskId\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"PartUsageHistories\" RENAME CONSTRAINT \"FK_PartUsageHistories_MaintenanceTasks_MaintenanceTaskId\" TO \"FK_PartUsageHistories_Works_WorkId\";");

            migrationBuilder.RenameIndex(
                name: "IX_PartUsageHistories_MaintenanceTaskId",
                table: "PartUsageHistories",
                newName: "IX_PartUsageHistories_WorkId");

            migrationBuilder.RenameColumn(
                name: "MaintenanceTaskId",
                table: "PartUsageHistories",
                newName: "WorkId");

            migrationBuilder.RenameIndex(
                name: "IX_MaintenanceTasks_UserId_ParentType_ParentId_IsActive",
                table: "MaintenanceTasks",
                newName: "IX_Works_UserId_ParentType_ParentId_IsActive");

            migrationBuilder.Sql("ALTER TABLE \"MaintenanceTasks\" RENAME CONSTRAINT \"FK_MaintenanceTasks_Users_UserId\" TO \"FK_Works_Users_UserId\";");
            migrationBuilder.Sql("ALTER TABLE \"MaintenanceTasks\" RENAME CONSTRAINT \"PK_MaintenanceTasks\" TO \"PK_Works\";");

            migrationBuilder.RenameTable(
                name: "MaintenanceTasks",
                newName: "Works");
        }
    }
}
