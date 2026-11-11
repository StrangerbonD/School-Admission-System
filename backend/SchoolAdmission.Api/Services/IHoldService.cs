using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Services;

public interface IHoldService
{
    Task<CreateHoldResponse> CreateHoldAsync(Guid classId, string studentId, CancellationToken cancellationToken = default);
    Task<HoldDetailsResponse> GetHoldByIdAsync(Guid holdId, CancellationToken cancellationToken = default);
    Task<ConfirmHoldResponse> ConfirmHoldAsync(Guid holdId, string studentId, CancellationToken cancellationToken = default);
    Task CancelHoldAsync(Guid holdId, string studentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentHoldSummaryResponse>> GetMyHoldsAsync(string studentId, CancellationToken cancellationToken = default);
}
