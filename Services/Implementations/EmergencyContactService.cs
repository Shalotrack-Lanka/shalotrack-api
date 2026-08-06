using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.EmergencyContact;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class EmergencyContactService : IEmergencyContactService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public EmergencyContactService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<EmergencyContactResponseDto>>> GetMyContactsAsync()
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
        {
            return ApiResponse<IReadOnlyList<EmergencyContactResponseDto>>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var contacts = await _unitOfWork.EmergencyContacts.GetByCustomerAsync(customer.CustomerId);
        var dtoList = contacts.Select(ToDto).ToList();

        return ApiResponse<IReadOnlyList<EmergencyContactResponseDto>>.Ok(dtoList, "Emergency contacts retrieved successfully.");
    }

    public async Task<ApiResponse<EmergencyContactResponseDto>> AddContactAsync(CreateEmergencyContactDto dto)
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
        {
            return ApiResponse<EmergencyContactResponseDto>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            return ApiResponse<EmergencyContactResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "Name and phone number are required.", "Both fields must be non-empty.");
        }

        var existingContacts = await _unitOfWork.EmergencyContacts.GetByCustomerAsync(customer.CustomerId);
        var limit = await GetContactLimitForCustomerAsync(customer.CustomerId);

        if (existingContacts.Count >= limit)
        {
            return ApiResponse<EmergencyContactResponseDto>.Fail(
                (int)HttpStatusCode.Forbidden,
                "Emergency contact limit reached.",
                $"Your current plan allows up to {limit} emergency contact(s). Upgrade your plan to add more.");
        }

        var contact = new EmergencyContact
        {
            EmergencyContactId = Guid.NewGuid(),
            CustomerId = customer.CustomerId,
            Name = dto.Name.Trim(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            Relationship = string.IsNullOrWhiteSpace(dto.Relationship) ? null : dto.Relationship.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.EmergencyContacts.AddAsync(contact);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<EmergencyContactResponseDto>.Ok(ToDto(contact), "Emergency contact added.");
    }

    public async Task<ApiResponse<string>> DeleteContactAsync(Guid emergencyContactId)
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var contact = await _unitOfWork.EmergencyContacts.GetByIdAsync(emergencyContactId);
        if (contact is null)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound, "Contact not found.", $"No emergency contact exists with ID '{emergencyContactId}'.");
        }

        // Same ownership pattern as VehicleService.GetByIdAsync: 404, not
        // 403, on a mismatch -- doesn't reveal to a caller that a contact
        // ID exists but belongs to someone else.
        if (!_currentUser.IsStaff && contact.CustomerId != customer.CustomerId)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound, "Contact not found.", $"No emergency contact exists with ID '{emergencyContactId}'.");
        }

        _unitOfWork.EmergencyContacts.Remove(contact);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<string>.Ok("OK", "Emergency contact removed.");
    }

    private async Task<Customer?> ResolveCustomerAsync()
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid)) return null;
        return await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
    }

    // PLACEHOLDER NUMBERS -- not client-specified. The old tier design had
    // real limits (Free=1, Silver=2, Gold=3, Platinum=5) but those mapped
    // to a pricing model that no longer exists (Free/Silver/Gold/Platinum
    // feature tiers, replaced with Free/1yr/2yr/3yr duration plans). These
    // values are a reasonable placeholder (limit increases with plan
    // length) so the feature is functional and demonstrable now, but MUST
    // be confirmed with the client before this is considered final -- do
    // not treat these numbers as approved requirements.
    private static int GetContactLimitForPlan(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Free => 1,
        SubscriptionPlan.OneYear => 3,
        SubscriptionPlan.TwoYears => 5,
        SubscriptionPlan.ThreeYears => 10,
        _ => 1
    };

    private async Task<int> GetContactLimitForCustomerAsync(Guid customerId)
    {
        var subscriptions = await _unitOfWork.Subscriptions.GetByCustomerAsync(customerId);
        var active = subscriptions.FirstOrDefault(s => s.Status == SubscriptionStatus.Active);
        // No active subscription at all (never subscribed) defaults to the
        // most conservative (Free-tier) limit, not zero and not unlimited.
        return active is null ? GetContactLimitForPlan(SubscriptionPlan.Free) : GetContactLimitForPlan(active.Plan);
    }

    private static EmergencyContactResponseDto ToDto(EmergencyContact contact)
    {
        return new EmergencyContactResponseDto
        {
            EmergencyContactId = contact.EmergencyContactId,
            Name = contact.Name,
            PhoneNumber = contact.PhoneNumber,
            Relationship = contact.Relationship,
            CreatedAt = contact.CreatedAt
        };
    }
}