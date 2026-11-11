using System.Net.Http.Json;
using ShaloTrack_API.DTOs.SetupShalotrackDevice;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class SetupShalotrackDeviceService : ISetupShalotrackDeviceService
{
    // Statuses the gateway refuses outright (see shalotrack-gateway services/device_access.py).
    private static readonly string[] StoppedStatuses =
        { "Temporarily Stopped", "Pending Repair", "Broken Device" };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SetupShalotrackDeviceService> _logger;

    public SetupShalotrackDeviceService(
        IUnitOfWork unitOfWork,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SetupShalotrackDeviceService> logger)
    {
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ApiResponse<string>> UpsertAsync(SyncSetupShalotrackDeviceDto dto)
    {
        // Upsert by IMEI — the natural real-world unique key for a physical
        // device, more reliable than trusting Admin's local integer ID to
        // stay meaningful across two separate databases.
        var existing = await _unitOfWork.SetupShalotrackDevices.GetByImeiAsync(dto.ImeiNumber);
        var previousStatus = existing?.Status;

        if (existing is null)
        {
            var device = new SetupShalotrackDevice
            {
                Id = dto.Id,
                DeviceCategory = dto.DeviceCategory,
                ImeiNumber = dto.ImeiNumber,
                SimNumber = dto.SimNumber,
                Status = dto.Status,
                CancelReason = dto.CancelReason,
                CanceledDate = dto.CanceledDate,
                DealerId = dto.DealerId,
                DeviceTypeId = dto.DeviceTypeId,
                BoundAt = ResolveBoundAt(dto, null),
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
            };

            await _unitOfWork.SetupShalotrackDevices.AddAsync(device);
        }
        else
        {
            existing.DeviceCategory = dto.DeviceCategory;
            existing.SimNumber = dto.SimNumber;
            existing.Status = dto.Status;
            existing.CancelReason = dto.CancelReason;
            existing.CanceledDate = dto.CanceledDate;
            existing.DealerId = dto.DealerId;
            existing.DeviceTypeId = dto.DeviceTypeId;
            existing.BoundAt = ResolveBoundAt(dto, existing.BoundAt);
            existing.UpdatedAt = dto.UpdatedAt;

            _unitOfWork.SetupShalotrackDevices.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync();

        // Only on the change INTO a stopped status. Hourly drift re-syncs of an already-stopped
        // device do not re-drop; the gateway's own 5-minute re-check is the backstop.
        if (StoppedStatuses.Contains(dto.Status) && !StoppedStatuses.Contains(previousStatus ?? string.Empty))
            await TryDropFromGatewayAsync(dto.ImeiNumber);

        return ApiResponse<string>.Ok("Device synced successfully.", "Device synced successfully.");
    }

    // Best effort: tell the gateway to disconnect the device now. The sync has already been saved,
    // so a gateway that is down, slow or older than this feature must never fail or slow the sync
    // (the ERP would otherwise retry forever). Switch off with Gateway:DropOnStop=false.
    private async Task TryDropFromGatewayAsync(string imei)
    {
        if (bool.TryParse(_configuration["Gateway:DropOnStop"], out var enabled) && !enabled) return;

        try
        {
            var baseUrl = _configuration["Gateway:CommandApiUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl)) return;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var client = _httpClientFactory.CreateClient("GatewayCommandClient");
            using var response = await client.PostAsJsonAsync(
                $"{baseUrl}/drop", new { imei, reason = "erp status stopped" }, cts.Token);

            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Gateway /drop for {Imei} returned HTTP {Status}", imei, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gateway /drop for {Imei} failed; the gateway re-check will catch it", imei);
        }
    }

    // Old Admin payloads (IsBound == null) never change the stored value, so
    // deploying the API before Admin is safe. FirstSeenAt is deliberately not
    // touched here: it belongs to the gateway.
    private static DateTime? ResolveBoundAt(SyncSetupShalotrackDeviceDto dto, DateTime? current)
    {
        if (dto.IsBound is null) return current;
        if (dto.IsBound == false) return null;
        return dto.BoundAt?.ToUniversalTime() ?? current ?? DateTime.UtcNow;
    }
}