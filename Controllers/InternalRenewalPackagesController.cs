using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.Renewals;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

// Protected like every /api/internal route: AdminSyncKeyMiddleware checks the shared key on the path
// prefix and marks the request trusted. The service re-checks IsStaff anyway.
[ApiController]
[Route("api/internal/renewal-packages")]
[AllowAnonymous]
public class InternalRenewalPackagesController : ControllerBase
{
    private readonly IRenewalPackageService _packages;

    public InternalRenewalPackagesController(IRenewalPackageService packages)
    {
        _packages = packages;
    }

    // The admin portal pushes its whole current price list. Idempotent: pushing twice changes nothing.
    [HttpPut]
    public async Task<IActionResult> Sync([FromBody] SyncRenewalPackagesDto dto, CancellationToken cancellationToken)
    {
        var response = await _packages.SyncAsync(dto, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}