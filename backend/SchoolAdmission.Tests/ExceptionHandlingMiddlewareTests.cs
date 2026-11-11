using System.IO;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SchoolAdmission.Api.Middleware;
using SchoolAdmission.Api.Models;
using Xunit;

namespace SchoolAdmission.Tests;

public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int StatusCode, ErrorResponse? Error)> ExecuteMiddlewareWithExceptionAsync(Exception ex)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        RequestDelegate next = _ => throw ex;
        var middleware = new ExceptionHandlingMiddleware(next, NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        var errorObj = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<ErrorResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return (context.Response.StatusCode, errorObj);
    }

    [Fact]
    public async Task TimeoutException_Returns503ServiceUnavailableJson()
    {
        var (status, error) = await ExecuteMiddlewareWithExceptionAsync(new TimeoutException("Database operation timed out"));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
        Assert.NotNull(error);
        Assert.Equal("service_unavailable", error.Error);
    }

    [Fact]
    public async Task NpgsqlException_ConnectionFailure_Returns503ServiceUnavailableJson()
    {
        var (status, error) = await ExecuteMiddlewareWithExceptionAsync(new NpgsqlException("Connection closed by remote host"));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
        Assert.NotNull(error);
        Assert.Equal("service_unavailable", error.Error);
    }

    [Fact]
    public async Task PostgresException_TooManyConnections_Returns503ServiceUnavailableJson()
    {
        var pgEx = new PostgresException("too many connections", "FATAL", "FATAL", "53300");
        var (status, error) = await ExecuteMiddlewareWithExceptionAsync(pgEx);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
        Assert.NotNull(error);
        Assert.Equal("service_unavailable", error.Error);
    }

    [Fact]
    public async Task PostgresException_UniqueViolation_Returns409AlreadyAppliedJson()
    {
        var pgEx = new PostgresException("duplicate key value", "ERROR", "ERROR", "23505");
        var (status, error) = await ExecuteMiddlewareWithExceptionAsync(pgEx);

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.NotNull(error);
        Assert.Equal("already_applied", error.Error);
    }

    [Fact]
    public async Task GenericUnhandledException_Returns500InternalErrorWithoutStackTrace()
    {
        var (status, error) = await ExecuteMiddlewareWithExceptionAsync(new InvalidOperationException("Sensitive internal crash info"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.NotNull(error);
        Assert.Equal("internal_error", error.Error);
        Assert.DoesNotContain("Sensitive internal crash info", error.Message);
    }
}
