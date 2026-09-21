using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Realtime;

/// <summary>
/// Drains ITripCloseEventQueue sequentially and calls ITripPurgeService for
/// each closed trip. Deliberately sequential, not parallel -- current fleet
/// size (2 devices) doesn't need concurrency, and adding a worker pool now
/// would be solving a scale problem this project doesn't have yet. If fleet
/// size grows enough that archival throughput becomes a real bottleneck,
/// bumping this to N parallel consumers is a small, contained change here --
/// nothing else needs to know about it.
///
/// Each item gets its own DI scope, so each trip's archive-and-purge work
/// gets its own ShaloTrackDbContext instance/connection -- required for
/// TripPurgeService's advisory lock, which must be held on one connection
/// for the whole operation (see TripPurgeService).
///
/// One bad trip does not stop this worker: exceptions are caught and logged
/// per item so a single failure (bad data, transient S3/DB error) doesn't
/// kill the loop for every trip after it.
/// </summary>
public class TripArchivalQueueWorker : BackgroundService
{
    private readonly ITripCloseEventQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TripArchivalQueueWorker> _logger;

    public TripArchivalQueueWorker(
        ITripCloseEventQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<TripArchivalQueueWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var tripCloseEvent in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var purgeService = scope.ServiceProvider.GetRequiredService<ITripPurgeService>();

                await purgeService.ArchiveAndPurgeTripAsync(
                    tripCloseEvent.DeviceId,
                    tripCloseEvent.VehicleId,
                    tripCloseEvent.TripEndTime,
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "TripArchivalQueueWorker: failed processing trip close for device {DeviceId} -- " +
                    "continuing with the next queued item, this one is dropped (not retried).",
                    tripCloseEvent.DeviceId);
            }
        }
    }
}