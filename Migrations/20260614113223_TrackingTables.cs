using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShaloTrack_API.Migrations
{
    /// <inheritdoc />
    public partial class TrackingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CurrentLocations",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric", nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric", nullable: false),
                    Speed = table.Column<decimal>(type: "numeric", nullable: false),
                    Heading = table.Column<decimal>(type: "numeric", nullable: false),
                    IgnitionStatus = table.Column<bool>(type: "boolean", nullable: false),
                    MovementStatus = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrentLocations", x => x.DeviceId);
                    table.ForeignKey(
                        name: "FK_CurrentLocations_GpsDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "GpsDevices",
                        principalColumn: "DeviceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CurrentLocations_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GpsTrackings",
                columns: table => new
                {
                    TrackingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric", nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric", nullable: false),
                    Altitude = table.Column<decimal>(type: "numeric", nullable: true),
                    Speed = table.Column<decimal>(type: "numeric", nullable: false),
                    Heading = table.Column<decimal>(type: "numeric", nullable: false),
                    Satellites = table.Column<int>(type: "integer", nullable: false),
                    GpsAccuracy = table.Column<decimal>(type: "numeric", nullable: true),
                    EventTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GpsTrackings", x => x.TrackingId);
                    table.ForeignKey(
                        name: "FK_GpsTrackings_GpsDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "GpsDevices",
                        principalColumn: "DeviceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RawPackets",
                columns: table => new
                {
                    PacketId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtocolNumber = table.Column<string>(type: "text", nullable: false),
                    RawHex = table.Column<string>(type: "text", nullable: false),
                    PacketLength = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Parsed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawPackets", x => x.PacketId);
                    table.ForeignKey(
                        name: "FK_RawPackets_GpsDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "GpsDevices",
                        principalColumn: "DeviceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurrentLocations_VehicleId",
                table: "CurrentLocations",
                column: "VehicleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GpsTrackings_DeviceId",
                table: "GpsTrackings",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_RawPackets_DeviceId",
                table: "RawPackets",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CurrentLocations");

            migrationBuilder.DropTable(
                name: "GpsTrackings");

            migrationBuilder.DropTable(
                name: "RawPackets");
        }
    }
}
