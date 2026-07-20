using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShaloTrack_API.Migrations
{
    public partial class AddDeviceStatusUpdateTrigger : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DeviceStatuses has no VehicleId column directly -- resolve it via the
            // device's currently active assignment (Status = 1 = Active) at trigger
            // time. If a device has no active assignment (unlinked hardware), no
            // notification is sent -- nothing to alert a customer about yet.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION notify_device_status_update() RETURNS trigger AS $$
                DECLARE
                    v_vehicle_id uuid;
                BEGIN
                    SELECT ""VehicleId"" INTO v_vehicle_id
                    FROM ""DeviceAssignments""
                    WHERE ""DeviceId"" = NEW.""DeviceId"" AND ""Status"" = 1
                    LIMIT 1;

                    IF v_vehicle_id IS NOT NULL THEN
                        PERFORM pg_notify(
                            'device_status_updates',
                            json_build_object(
                                'vehicleId', v_vehicle_id,
                                'deviceId', NEW.""DeviceId"",
                                'batteryLevel', NEW.""BatteryLevel"",
                                'powerStatus', NEW.""PowerStatus"",
                                'ignitionStatus', NEW.""IgnitionStatus"",
                                'isOnline', NEW.""IsOnline"",
                                'updatedAt', NEW.""UpdatedAt""
                            )::text
                        );
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS trg_notify_device_status_update ON ""DeviceStatuses"";
                CREATE TRIGGER trg_notify_device_status_update
                AFTER INSERT OR UPDATE ON ""DeviceStatuses""
                FOR EACH ROW EXECUTE FUNCTION notify_device_status_update();
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_notify_device_status_update ON ""DeviceStatuses"";");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS notify_device_status_update();");
        }
    }
}