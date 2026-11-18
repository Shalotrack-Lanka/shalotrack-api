using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShaloTrack_API.Migrations
{
    /// <summary>
    /// Per-vehicle push on/off (Alert settings). Additive only: four NOT NULL boolean columns on Vehicles with
    /// DEFAULT true, so every existing vehicle keeps getting every push it gets today. On PostgreSQL 11+ a
    /// constant default is a metadata-only change: no table rewrite, no long lock.
    /// </summary>
    public partial class AddVehiclePushSwitches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PushIgnition",
                table: "Vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PushOverspeed",
                table: "Vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PushPowerCut",
                table: "Vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PushTrackerHealth",
                table: "Vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PushIgnition", table: "Vehicles");
            migrationBuilder.DropColumn(name: "PushOverspeed", table: "Vehicles");
            migrationBuilder.DropColumn(name: "PushPowerCut", table: "Vehicles");
            migrationBuilder.DropColumn(name: "PushTrackerHealth", table: "Vehicles");
        }
    }
}
