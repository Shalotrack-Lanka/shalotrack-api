using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShaloTrack_API.Migrations
{
    /// <inheritdoc />
    public partial class AddArchivalIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_RawPackets_DeviceId_ReceivedAt\" ON \"RawPackets\" (\"DeviceId\", \"ReceivedAt\");",
            suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS \"IX_RawPackets_DeviceId\";",
                suppressTransaction: true);

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_DeviceId_AlertType_TriggeredAt",
                table: "Alerts",
                columns: new[] { "DeviceId", "AlertType", "TriggeredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RawPackets_DeviceId_ReceivedAt",
                table: "RawPackets");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_DeviceId_AlertType_TriggeredAt",
                table: "Alerts");

            migrationBuilder.CreateIndex(
                name: "IX_RawPackets_DeviceId",
                table: "RawPackets",
                column: "DeviceId");
        }
    }
}
