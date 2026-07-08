namespace ShaloTrack_API.DTOs.Dashboard;

public class DashboardResponseDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int VehicleCount { get; set; }
    public int OnlineVehicles { get; set; }
    public int OfflineVehicles { get; set; }
    public List<DashboardVehicleDto> Vehicles { get; set; }
        = new();
}