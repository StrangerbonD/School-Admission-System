using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Npgsql;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);

            if (!context.Response.HasStarted && context.Response.ContentType == null)
            {
                if (context.Response.StatusCode == StatusCodes.Status404NotFound)
                {
                    await WriteJsonResponseAsync(context, StatusCodes.Status404NotFound, "not_found", "The requested resource was not found.");
                }
                else if (context.Response.StatusCode == StatusCodes.Status405MethodNotAllowed)
                {
                    await WriteJsonResponseAsync(context, StatusCodes.Status405MethodNotAllowed, "method_not_allowed", "The HTTP method is not allowed for this endpoint.");
                }
                else if (context.Response.StatusCode == StatusCodes.Status415UnsupportedMediaType)
                {
                    await WriteJsonResponseAsync(context, StatusCodes.Status415UnsupportedMediaType, "validation_error", "Unsupported media type. JSON is required.");
                }
                else if (context.Response.StatusCode == StatusCodes.Status400BadRequest)
                {
                    await WriteJsonResponseAsync(context, StatusCodes.Status400BadRequest, "validation_error", "Invalid request body or parameters.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499;
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("The response has already started, cannot write exception response: {Message}", ex.Message);
            return;
        }

        if (ex is AppException appEx)
        {
            _logger.LogWarning(appEx, "Application exception: {ErrorCode} - {Message}", appEx.ErrorCode, appEx.Message);
            await WriteJsonResponseAsync(context, appEx.StatusCode, appEx.ErrorCode, appEx.Message);
            return;
        }

        if (ex is BadHttpRequestException badHttpEx)
        {
            _logger.LogWarning(badHttpEx, "Bad HTTP request: {Message}", badHttpEx.Message);
            var status = badHttpEx.StatusCode != 0 ? badHttpEx.StatusCode : 400;
            var code = status == 415 ? "validation_error" : "validation_error";
            await WriteJsonResponseAsync(context, status, code, "Invalid request body, format, or headers.");
            return;
        }

        if (ex is JsonException jsonEx)
        {
            _logger.LogWarning(jsonEx, "JSON deserialization failed: {Message}", jsonEx.Message);
            await WriteJsonResponseAsync(context, StatusCodes.Status400BadRequest, "validation_error", "Malformed or invalid JSON body.");
            return;
        }

        var dbEx = FindExceptionInChain<NpgsqlException>(ex);
        if (dbEx is PostgresException pgEx)
        {
            _logger.LogError(pgEx, "PostgresException: SqlState {SqlState} - {Message}", pgEx.SqlState, pgEx.Message);

            switch (pgEx.SqlState)
            {
                case "23505":
                    await WriteJsonResponseAsync(context, StatusCodes.Status409Conflict, "already_applied", "An active hold or confirmed admission already exists for this student.");
                    return;

                case "57014":
                case "55P03":
                case "53300":
                    await WriteJsonResponseAsync(context, StatusCodes.Status503ServiceUnavailable, "service_unavailable", "Database service is temporarily unavailable. Please try again.");
                    return;

                default:
                    await WriteJsonResponseAsync(context, StatusCodes.Status500InternalServerError, "internal_error", "A database error occurred.");
                    return;
            }
        }

        if (dbEx != null)
        {
            _logger.LogError(dbEx, "Database connection error: {Message}", dbEx.Message);
            await WriteJsonResponseAsync(context, StatusCodes.Status503ServiceUnavailable, "service_unavailable", "Database connection error. Please try again later.");
            return;
        }

        var timeoutEx = FindExceptionInChain<TimeoutException>(ex);
        if (timeoutEx != null)
        {
            _logger.LogError(timeoutEx, "Timeout occurred: {Message}", timeoutEx.Message);
            await WriteJsonResponseAsync(context, StatusCodes.Status503ServiceUnavailable, "service_unavailable", "The operation timed out. Please try again.");
            return;
        }

        _logger.LogError(ex, "Unhandled server exception: {Message}", ex.Message);
        await WriteJsonResponseAsync(context, StatusCodes.Status500InternalServerError, "internal_error", "An internal server error occurred.");
    }

    private static T? FindExceptionInChain<T>(Exception? ex) where T : Exception
    {
        while (ex != null)
        {
            if (ex is T matched) return matched;
            ex = ex.InnerException;
        }
        return null;
    }

    private static async Task WriteJsonResponseAsync(HttpContext context, int statusCode, string errorCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse(errorCode, message);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
