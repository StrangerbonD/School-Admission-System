using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Endpoints;

public static class HoldsEndpoints
{
    public static IEndpointRouteBuilder MapHoldsEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/classes/{classId:guid}/holds", async (
            Guid classId,
            HttpContext httpContext,
            IHoldService holdService,
            CancellationToken ct) =>
        {
            var studentId = StudentIdValidator.ExtractAndValidate(httpContext);
            var result = await holdService.CreateHoldAsync(classId, studentId, ct);
            return Results.Created($"/holds/{result.HoldId}", result);
        });

        routes.MapGet("/holds/{holdId:guid}", async (
            Guid holdId,
            IHoldService holdService,
            CancellationToken ct) =>
        {
            var result = await holdService.GetHoldByIdAsync(holdId, ct);
            return Results.Ok(result);
        });

        routes.MapPost("/holds/{holdId:guid}/confirm", async (
            Guid holdId,
            HttpContext httpContext,
            IHoldService holdService,
            CancellationToken ct) =>
        {
            var studentId = StudentIdValidator.ExtractAndValidate(httpContext);
            var result = await holdService.ConfirmHoldAsync(holdId, studentId, ct);
            return Results.Ok(result);
        });

        routes.MapDelete("/holds/{holdId:guid}", async (
            Guid holdId,
            HttpContext httpContext,
            IHoldService holdService,
            CancellationToken ct) =>
        {
            var studentId = StudentIdValidator.ExtractAndValidate(httpContext);
            await holdService.CancelHoldAsync(holdId, studentId, ct);
            return Results.NoContent();
        });

        routes.MapGet("/me/holds", async (
            HttpContext httpContext,
            IHoldService holdService,
            CancellationToken ct) =>
        {
            var studentId = StudentIdValidator.ExtractAndValidate(httpContext);
            var results = await holdService.GetMyHoldsAsync(studentId, ct);
            return Results.Ok(results);
        });

        return routes;
    }
}
