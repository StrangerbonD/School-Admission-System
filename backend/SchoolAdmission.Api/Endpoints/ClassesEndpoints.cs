using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.Models;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Endpoints;

public static class ClassesEndpoints
{
    public static IEndpointRouteBuilder MapClassesEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/classes");

        group.MapPost("", async (CreateClassRequest? request, IClassService classService, CancellationToken ct) =>
        {
            if (request == null)
            {
                throw new ValidationException("Request body cannot be null or empty.");
            }

            var created = await classService.CreateClassAsync(request, ct);
            return Results.Created($"/classes/{created.Id}", created);
        });

        group.MapGet("", async (IClassService classService, CancellationToken ct) =>
        {
            var classes = await classService.GetAllClassesAsync(ct);
            return Results.Ok(classes);
        });

        group.MapGet("/{classId:guid}", async (Guid classId, IClassService classService, CancellationToken ct) =>
        {
            var summary = await classService.GetClassByIdAsync(classId, ct);
            if (summary == null)
            {
                throw new NotFoundException($"Class with id '{classId}' not found.");
            }

            return Results.Ok(summary);
        });

        return routes;
    }
}
