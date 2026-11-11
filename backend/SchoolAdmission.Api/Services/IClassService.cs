using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Services;

public interface IClassService
{
    Task<ClassResponse> CreateClassAsync(CreateClassRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClassSummaryResponse>> GetAllClassesAsync(CancellationToken cancellationToken = default);
    Task<ClassSummaryResponse?> GetClassByIdAsync(Guid classId, CancellationToken cancellationToken = default);
}
