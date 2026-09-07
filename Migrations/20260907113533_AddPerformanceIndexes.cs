using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShaloTrack_API.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GpsTrackings_DeviceId",
                table: "GpsTrackings");

            migrationBuilder.CreateIndex(
                name: "IX_GpsTrackings_DeviceId_EventTime",
                table: "GpsTrackings",
                columns: new[] { "DeviceId", "EventTime" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GpsTrackings_DeviceId_EventTime",
                table: "GpsTrackings");

            migrationBuilder.CreateIndex(
                name: "IX_GpsTrackings_DeviceId",
                table: "GpsTrackings",
                column: "DeviceId");
        }
    }
}
