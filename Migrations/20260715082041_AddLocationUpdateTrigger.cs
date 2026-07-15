using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShaloTrack_API.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationUpdateTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION notify_location_update() RETURNS trigger AS $$
                BEGIN
                    PERFORM pg_notify(
                        'location_updates',
                        json_build_object(
                            'vehicleId', NEW.""VehicleId"",
                            'deviceId', NEW.""DeviceId"",
                            'latitude', NEW.""Latitude"",
                            'longitude', NEW.""Longitude"",
                            'speed', NEW.""Speed"",
                            'heading', NEW.""Heading"",
                            'ignitionStatus', NEW.""IgnitionStatus"",
                            'movementStatus', NEW.""MovementStatus"",
                            'lastUpdate', NEW.""LastUpdate""
                        )::text
                    );
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS trg_notify_location_update ON ""CurrentLocations"";
                CREATE TRIGGER trg_notify_location_update
                AFTER INSERT OR UPDATE ON ""CurrentLocations""
                FOR EACH ROW EXECUTE FUNCTION notify_location_update();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_notify_location_update ON ""CurrentLocations"";");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS notify_location_update();");
        }
    }
}