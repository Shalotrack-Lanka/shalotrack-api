using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShaloTrack_API.Migrations
{
    /// <inheritdoc />
    public partial class AddSetupShalotrackDevicesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SetupShalotrackDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DeviceCategory = table.Column<string>(type: "text", nullable: false),
                    ImeiNumber = table.Column<string>(type: "text", nullable: false),
                    SimNumber = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CancelReason = table.Column<string>(type: "text", nullable: true),
                    CanceledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DealerId = table.Column<int>(type: "integer", nullable: true),
                    DeviceTypeId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetupShalotrackDevices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SetupShalotrackDevices_ImeiNumber",
                table: "SetupShalotrackDevices",
                column: "ImeiNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SetupShalotrackDevices");
        }
    }
}
