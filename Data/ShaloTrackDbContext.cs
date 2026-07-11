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
    public DbSet<RawPacket> RawPackets => Set<RawPacket>();
    public DbSet<GpsTracking> GpsTrackings => Set<GpsTracking>();
    public DbSet<CurrentLocation> CurrentLocations => Set<CurrentLocation>();
    public DbSet<DeviceEvent> DeviceEvents => Set<DeviceEvent>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>()
            .HasIndex(c => c.FirebaseUid)
            .IsUnique();

        modelBuilder.Entity<CurrentLocation>()
            .HasKey(c => c.DeviceId);

        modelBuilder.Entity<CurrentLocation>()
            .HasOne(c => c.Device)
            .WithOne(d => d.CurrentLocation)
            .HasForeignKey<CurrentLocation>(c => c.DeviceId);

        modelBuilder.Entity<CurrentLocation>()
            .HasOne(c => c.Vehicle)
            .WithOne(v => v.CurrentLocation)
            .HasForeignKey<CurrentLocation>(c => c.VehicleId);

        modelBuilder.Entity<RawPacket>()
            .HasOne(r => r.Device)
            .WithMany(d => d.RawPackets)
            .HasForeignKey(r => r.DeviceId);

        modelBuilder.Entity<GpsTracking>()
            .HasOne(g => g.Device)
            .WithMany(d => d.GpsTrackings)
            .HasForeignKey(g => g.DeviceId);

        modelBuilder.Entity<DeviceAssignment>()
            .HasOne(a => a.Vehicle)
            .WithMany(v => v.DeviceAssignments)
            .HasForeignKey(a => a.VehicleId);

        modelBuilder.Entity<DeviceAssignment>()
            .HasOne(a => a.Device)
            .WithMany(d => d.DeviceAssignments)
            .HasForeignKey(a => a.DeviceId);

        modelBuilder.Entity<DeviceStatus>()
            .HasOne(d => d.Device)
            .WithOne()
            .HasForeignKey<DeviceStatus>(d => d.DeviceId);

        modelBuilder.Entity<DeviceEvent>()
            .HasOne(e => e.Device)
            .WithMany(d => d.DeviceEvents)
            .HasForeignKey(e => e.DeviceId);    

        modelBuilder.Entity<DeviceEvent>()
            .HasOne(e => e.Vehicle)
            .WithMany(v => v.DeviceEvents)
            .HasForeignKey(e => e.VehicleId);

        modelBuilder.Entity<DeviceEvent>()
            .HasOne(e => e.RawPacket)
            .WithMany(r => r.DeviceEvents)
            .HasForeignKey(e => e.RawPacketId);
    }
}