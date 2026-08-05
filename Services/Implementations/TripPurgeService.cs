using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class TripPurgeService : ITripPurgeService
{
    private readonly ShaloTrackDbContext _context;
    private readonly ITripArchivalService _archivalService;
    private readonly IGpsTrackingRepository _gpsTrackingRepository;
    private readonly IRawPacketRepository _rawPacketRepository;
    private readonly bool _dryRun;
    private readonly ILogger<TripPurgeService> _logger;

    public TripPurgeService(
        ShaloTrackDbContext context,
        ITripArchivalService archivalService,
        IGpsTrackingRepository gpsTrackingRepository,
        IRawPacketRepository rawPacketRepository,
        IConfiguration configuration,
        ILogger<TripPurgeService> logger)
    {
        _context = context;
        _archivalService = archivalService;
        _gpsTrackingRepository = gpsTrackingRepository;
        _rawPacketRepository = rawPacketRepository;
        _logger = logger;

        // Defaults TRUE -- fails safe. A missing or misspelled config key
        // means "don't delete anything", not the other way around. Must be
        // explicitly set to false once dry-run output has been reviewed and
        // trusted -- see chat for how to check that.
        _dryRun = configuration.GetValue("GpsArchive:PurgeDryRun", true);
    }

    public async Task ArchiveAndPurgeTripAsync(
        Guid deviceId,
        Guid vehicleId,
        DateTime tripEndTime,
        CancellationToken cancellationToken = default)
    {
        // Advisory locks are session-scoped -- must be acquired and released
        // on the SAME physical connection for the whole operation. This
        // service, ITripArchivalService, IUnitOfWork.Alerts, and both
        // repositories below are all resolved from the same DI scope (see
        // TripArchivalQueueWorker), so they all share this exact
        // ShaloTrackDbContext instance and therefore this exact connection --
        // opened explicitly here instead of letting EF Core open/close it per
        // command, which is what makes the lock actually cover the whole
        // archive-then-delete sequence, not just one statement of it.
        await _context.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            var lockAcquired = await TryAcquireAdvisoryLockAsync(deviceId, cancellationToken);
            if (!lockAcquired)
            {
                _logger.LogInformation(
                    "TripPurgeService: advisory lock for device {DeviceId} already held (another instance is " +
                    "likely processing the same NOTIFY right now) -- skipping. Not a failure.",
                    deviceId);
                return;
            }

            try
            {
                var archiveResult = await _archivalService.ArchiveTripAsync(deviceId, vehicleId, tripEndTime, cancellationToken);

                if (!archiveResult.Success)
                {
                    _logger.LogError(
                        "TripPurgeService: archive step failed for device {DeviceId} ({Error}) -- nothing deleted.",
                        deviceId, archiveResult.ErrorMessage);
                    return;
                }

                if (archiveResult.PointCount == 0 || archiveResult.TripStart is null)
                {
                    _logger.LogInformation(
                        "TripPurgeService: nothing archived for device {DeviceId} -- nothing to purge.",
                        deviceId);
                    return;
                }

                var tripStart = archiveResult.TripStart.Value;

                if (_dryRun)
                {
                    var wouldDeleteGps = await _gpsTrackingRepository.CountByDeviceInRangeAsync(deviceId, tripStart, tripEndTime);
                    var wouldDeleteRaw = await _rawPacketRepository.CountByDeviceInRangeAsync(deviceId, tripStart, tripEndTime);

                    _logger.LogWarning(
                        "TripPurgeService: DRY RUN -- would delete {GpsCount} GpsTrackings row(s) and {RawCount} " +
                        "RawPackets row(s) for device {DeviceId} in [{From}, {To}] (archived to {S3Key}). Nothing " +
                        "was actually deleted. Set GpsArchive:PurgeDryRun=false to enable real deletes.",
                        wouldDeleteGps, wouldDeleteRaw, deviceId, tripStart, tripEndTime, archiveResult.S3Key);
                    return;
                }

                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var deletedGps = await _gpsTrackingRepository.DeleteByDeviceInRangeAsync(deviceId, tripStart, tripEndTime);
                    var deletedRaw = await _rawPacketRepository.DeleteByDeviceInRangeAsync(deviceId, tripStart, tripEndTime);

                    await transaction.CommitAsync(cancellationToken);

                    _logger.LogInformation(
                        "TripPurgeService: purged {GpsCount} GpsTrackings row(s) and {RawCount} RawPackets row(s) " +
                        "for device {DeviceId} in [{From}, {To}], archived to {S3Key}.",
                        deletedGps, deletedRaw, deviceId, tripStart, tripEndTime, archiveResult.S3Key);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }
            finally
            {
                await ReleaseAdvisoryLockAsync(deviceId, cancellationToken);
            }
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    private async Task<bool> TryAcquireAdvisoryLockAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(hashtext(@deviceId))";
        var param = command.CreateParameter();
        param.ParameterName = "@deviceId";
        param.Value = deviceId.ToString();
        command.Parameters.Add(param);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool acquired && acquired;
    }

    private async Task ReleaseAdvisoryLockAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_unlock(hashtext(@deviceId))";
        var param = command.CreateParameter();
        param.ParameterName = "@deviceId";
        param.Value = deviceId.ToString();
        command.Parameters.Add(param);

        await command.ExecuteScalarAsync(cancellationToken);
    }
}