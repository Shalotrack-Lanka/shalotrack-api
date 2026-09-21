using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Services.Realtime;

/// <summary>
/// Periodically deletes RawPackets rows older than a configurable retention
/// window, purely on ReceivedAt (the gateway's own ingestion wall-clock).
/// Deliberately decoupled from trip archival/purge.
///
/// INCIDENT NOTE (2026-09-22): this used to be done inside TripPurgeService,
/// deleting a device's RawPackets over the same [tripStart, tripEndTime]
/// window used to purge its GpsTrackings. That was wrong on two independent
/// counts:
///
///   1. RawPackets is not trip-scoped data. The gateway writes a row here
///      for EVERY protocol type it receives -- GPS location pings, but also
///      heartbeats, status updates, alarms, logins, command acks and
///      configuration packets (see shalotrack-gateway/services/
///      packet_handler.py). Deleting "this device's RawPackets between
///      trip start and trip end" deletes heartbeats/status/alarms that
///      happen to fall in that window too, which have no relationship to
///      "a trip" as a concept.
///
///   2. Even limited to GPS packets, the window itself used the wrong
///      clock. tripStart/tripEndTime come from GpsTracking.EventTime -- the
///      timestamp the DEVICE itself reports inside the GPS payload.
///      RawPackets.ReceivedAt is set by the gateway
///      (raw_packet_repository.py: datetime.now(timezone.utc)) at the
///      moment the TCP packet was received. These are two different
///      clocks with no enforced relationship. For an always-online device
///      they're usually seconds apart, which is why this went unnoticed --
///      but any device that buffers while out of GSM coverage and bursts
///      its backlog later can have EventTime and ReceivedAt hours apart,
///      which would silently delete the wrong rows (or miss the right
///      ones) with no error and no way to tell after the fact.
///
/// RawPackets are raw hex protocol payloads -- their only real value is
/// replaying one to debug a parser bug shortly after it happened. Once
/// GpsTracking/DeviceEvents have been derived from them (which happens
/// inline at ingestion, not later), they carry no further product value.
/// That's why this is a flat retention sweep, not an archive-then-delete
/// pipeline like TripPurgeService -- there's nothing here worth paying S3
/// storage and archive-read complexity to preserve.
///
/// Gated by RawPacketRetention:DryRun -- defaults to true (fails safe: logs
/// what WOULD be deleted, deletes nothing) until explicitly turned off, same
/// convention as GpsArchive:PurgeDryRun in TripPurgeService.
/// </summary>
public class RawPacketRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RawPacketRetentionWorker> _logger;

    private readonly TimeSpan _interval;
    private readonly int _retentionDays;
    private readonly int _batchSize;
    private readonly bool _dryRun;

    public RawPacketRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<RawPacketRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _interval = TimeSpan.FromHours(configuration.GetValue("RawPacketRetention:IntervalHours", 6));
        _retentionDays = configuration.GetValue("RawPacketRetention:RetentionDays", 14);
        _batchSize = configuration.GetValue("RawPacketRetention:BatchSize", 500);

        // Defaults TRUE -- fails safe. A missing or misspelled config key
        // means "don't delete anything", not the other way around. Must be
        // explicitly set to false once dry-run output has been reviewed and
        // trusted -- identical convention to TripPurgeService.PurgeDryRun.
        _dryRun = configuration.GetValue("RawPacketRetention:DryRun", true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "RawPacketRetentionWorker: sweep failed -- will retry on the next {IntervalHours}h tick.",
                    _interval.TotalHours);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var rawPacketRepository = scope.ServiceProvider.GetRequiredService<IRawPacketRepository>();

        var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);

        if (_dryRun)
        {
            var wouldDelete = await rawPacketRepository.CountOlderThanAsync(cutoff);
            _logger.LogWarning(
                "RawPacketRetentionWorker: DRY RUN -- would delete {Count} RawPackets row(s) older than " +
                "{Cutoff:O} ({RetentionDays} day retention). Nothing was actually deleted. Set " +
                "RawPacketRetention:DryRun=false to enable real deletes.",
                wouldDelete, cutoff, _retentionDays);
            return;
        }

        int totalDeleted = 0;
        int deletedThisBatch;

        do
        {
            deletedThisBatch = await rawPacketRepository.DeleteOldestBatchAsync(cutoff, _batchSize);
            totalDeleted += deletedThisBatch;

            if (deletedThisBatch == _batchSize)
            {
                // More rows likely remain past this batch -- brief pause
                // before the next one so this doesn't hammer Postgres with
                // back-to-back deletes on a t3.micro instance.
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            }
        }
        while (deletedThisBatch == _batchSize);

        if (totalDeleted > 0)
        {
            _logger.LogInformation(
                "RawPacketRetentionWorker: purged {Count} RawPackets row(s) older than {Cutoff:O} " +
                "({RetentionDays} day retention).",
                totalDeleted, cutoff, _retentionDays);
        }
    }
}