using ShaloTrack_API.DTOs.EmergencyContact;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IEmergencyContactService
{
    Task<ApiResponse<IReadOnlyList<EmergencyContactResponseDto>>> GetMyContactsAsync();
    Task<ApiResponse<EmergencyContactResponseDto>> AddContactAsync(CreateEmergencyContactDto dto);
    Task<ApiResponse<string>> DeleteContactAsync(Guid emergencyContactId);
}