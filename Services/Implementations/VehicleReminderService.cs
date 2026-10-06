using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.VehicleReminder;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Owner-only CRUD for vehicle reminders. Anything that is not the caller's own, active,
/// non-demo vehicle answers 404 (never 403), so IDs cannot be probed. Shared viewers and the
/// demo vehicle cannot create reminders: this is the owner's private data and the push goes to
/// the owner.
/// </summary>
public class VehicleReminderService : IVehicleReminderService
{
    private const int MaxNotesLength = 200;
    private const int MaxPastDays = 365;
    private const int MaxFutureDays = 3650;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public VehicleReminderService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<VehicleReminderResponseDto>>> GetForVehicleAsync(Guid vehicleId)
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
            return ApiResponse<IReadOnlyList<VehicleReminderResponseDto>>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");

        if (!await OwnsVehicleAsync(customer, vehicleId))
            return ApiResponse<IReadOnlyList<VehicleReminderResponseDto>>.Fail(
                (int)HttpStatusCode.NotFound, "Vehicle not found.", "No such vehicle.");

        var today = VehicleReminderNotifier.LocalToday(DateTime.UtcNow);
        var rows = await _unitOfWork.VehicleReminders.GetByVehicleAsync(vehicleId);

        return ApiResponse<IReadOnlyList<VehicleReminderResponseDto>>.Ok(
            rows.Select(r => ToDto(r, today)).ToList(), "Reminders retrieved successfully.");
    }

    public async Task<ApiResponse<VehicleReminderResponseDto>> UpsertAsync(Guid vehicleId, UpsertVehicleReminderDto dto)
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
            return ApiResponse<VehicleReminderResponseDto>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");

        if (!Enum.IsDefined(typeof(VehicleReminderType), dto.Type))
            return ApiResponse<VehicleReminderResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "Invalid reminder type.", "Type must be 0, 1 or 2.");

        var now = DateTime.UtcNow;
        var today = VehicleReminderNotifier.LocalToday(now);

        if (dto.DueDate < today.AddDays(-MaxPastDays) || dto.DueDate > today.AddDays(MaxFutureDays))
            return ApiResponse<VehicleReminderResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "Invalid date.", "Due date is out of the allowed range.");

        var notes = CleanNotes(dto.Notes);
        if (notes is not null && notes.Length > MaxNotesLength)
            return ApiResponse<VehicleReminderResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "Note is too long.", $"Notes can be at most {MaxNotesLength} characters.");

        if (!await OwnsVehicleAsync(customer, vehicleId))
            return ApiResponse<VehicleReminderResponseDto>.Fail(
                (int)HttpStatusCode.NotFound, "Vehicle not found.", "No such vehicle.");

        var type = (VehicleReminderType)dto.Type;
        var reminder = await _unitOfWork.VehicleReminders.GetByVehicleAndTypeAsync(vehicleId, type);

        if (reminder is null)
        {
            reminder = new VehicleReminder
            {
                ReminderId = Guid.NewGuid(),
                VehicleId = vehicleId,
                Type = type,
                DueDate = dto.DueDate,
                Notes = notes,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await _unitOfWork.VehicleReminders.AddAsync(reminder);
        }
        else
        {
            reminder.DueDate = dto.DueDate;
            reminder.Notes = notes;
            reminder.UpdatedAt = now;
        }

        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<VehicleReminderResponseDto>.Ok(ToDto(reminder, today), "Reminder saved.");
    }

    public async Task<ApiResponse<string>> DeleteAsync(Guid reminderId)
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");

        var reminder = await _unitOfWork.VehicleReminders.GetByIdWithVehicleAsync(reminderId);
        if (reminder is null || reminder.Vehicle.CustomerId != customer.CustomerId)
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound, "Reminder not found.", "No such reminder.");

        _unitOfWork.VehicleReminders.Remove(reminder);
        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<string>.Ok("OK", "Reminder removed.");
    }

    private async Task<bool> OwnsVehicleAsync(Customer customer, Guid vehicleId)
    {
        var vehicle = await _unitOfWork.Vehicles.GetByIdForOwnershipCheckAsync(vehicleId);
        return vehicle is not null
               && vehicle.IsActive
               && !vehicle.IsDemoVehicle
               && vehicle.CustomerId == customer.CustomerId;
    }

    private async Task<Customer?> ResolveCustomerAsync()
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid)) return null;
        return await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
    }

    /// <summary>Trim, drop control characters (stops log/lock-screen tricks), empty becomes null.</summary>
    private static string? CleanNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;
        var cleaned = new string(notes.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    private static VehicleReminderResponseDto ToDto(VehicleReminder r, DateOnly today) => new()
    {
        ReminderId = r.ReminderId,
        VehicleId = r.VehicleId,
        Type = (int)r.Type,
        DueDate = r.DueDate,
        DaysLeft = r.DueDate.DayNumber - today.DayNumber,
        Notes = r.Notes,
    };
}