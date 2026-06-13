using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Models;

namespace ShaloTrack_API.Data;

public class ShaloTrackDbContext : DbContext
{
    public ShaloTrackDbContext(
        DbContextOptions<ShaloTrackDbContext> options)
        : base(options)
    {
    }
    //Tables
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<GpsDevice> GpsDevices => Set<GpsDevice>();
    public DbSet<DeviceAssignment> DeviceAssignments => Set<DeviceAssignment>();
    public DbSet<DeviceStatus> DeviceStatuses => Set<DeviceStatus>();
}