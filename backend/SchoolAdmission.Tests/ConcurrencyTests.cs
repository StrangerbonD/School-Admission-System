using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Tests;

public class ConcurrencyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ConcurrencyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string GenerateStudentId(int index)
    {
        return $"{90000000000000000L + index:D17}";
    }

    [Fact]
    public async Task Test1_Concurrency_10Seats_200ParallelRequests_Exactly10Succeed_NoOverbooking()
    {
        var uniqueClassName = $"Concurrency 10-Seat Class {Guid.NewGuid():N}";
        var classResponse = await _client.PostAsJsonAsync("/classes", new CreateClassRequest(uniqueClassName, 10));
        classResponse.EnsureSuccessStatusCode();
        var cls = await classResponse.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(cls);

        const int concurrentRequests = 200;
        var tasks = new Task<HttpResponseMessage>[concurrentRequests];

        for (int i = 0; i < concurrentRequests; i++)
        {
            var studentId = GenerateStudentId(i);
            var req = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
            req.Headers.Add(StudentIdValidator.HeaderName, studentId);
            tasks[i] = _client.SendAsync(req);
        }

        var responses = await Task.WhenAll(tasks);

        var successResponses = responses.Where(r => r.StatusCode == HttpStatusCode.Created).ToList();
        var conflictResponses = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();
        var otherResponses = responses.Where(r => r.StatusCode != HttpStatusCode.Created && r.StatusCode != HttpStatusCode.Conflict).ToList();

        Assert.Empty(otherResponses);
        Assert.Equal(10, successResponses.Count);
        Assert.Equal(190, conflictResponses.Count);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var activeHoldsInDb = await db.Database.SqlQueryRaw<int>(@"
            SELECT count(*)::int AS ""Value"" FROM holds
            WHERE class_id = {0}
              AND (status = 'CONFIRMED' OR (status = 'HELD' AND expires_at > clock_timestamp()))", cls.Id)
            .SingleAsync();

        Assert.Equal(10, activeHoldsInDb);
    }

    [Fact]
    public async Task Test2_Concurrency_SameStudent_20ParallelRequests_Exactly1Succeeds()
    {
        var uniqueClassName = $"SameStudent Parallel Class {Guid.NewGuid():N}";
        var classResponse = await _client.PostAsJsonAsync("/classes", new CreateClassRequest(uniqueClassName, 10));
        classResponse.EnsureSuccessStatusCode();
        var cls = await classResponse.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(cls);

        var sameStudentId = "88888888888888888";
        const int parallelRequests = 20;
        var tasks = new Task<HttpResponseMessage>[parallelRequests];

        for (int i = 0; i < parallelRequests; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
            req.Headers.Add(StudentIdValidator.HeaderName, sameStudentId);
            tasks[i] = _client.SendAsync(req);
        }

        var responses = await Task.WhenAll(tasks);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        var others = responses.Where(r => r.StatusCode != HttpStatusCode.Created && r.StatusCode != HttpStatusCode.Conflict).ToList();

        Assert.Empty(others);
        Assert.Equal(1, successCount);
        Assert.Equal(19, conflictCount);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var studentHoldCount = await db.Holds
            .CountAsync(h => h.ClassId == cls.Id && h.StudentId == sameStudentId && (h.Status == "HELD" || h.Status == "CONFIRMED"));

        Assert.Equal(1, studentHoldCount);
    }

    [Fact]
    public async Task Test10_TimeFormat_ExpiresAtAndServerTime_EndsWithZ_AndMatchesHoldDuration()
    {
        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest("Time Format Class", 10));
        classRes.EnsureSuccessStatusCode();
        var cls = await classRes.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(cls);

        var studentId = "77777777777777777";
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
        req.Headers.Add(StudentIdValidator.HeaderName, studentId);

        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(rawJson);

        var expiresAtStr = doc.RootElement.GetProperty("expiresAt").GetString();
        var serverTimeStr = doc.RootElement.GetProperty("serverTime").GetString();

        Assert.NotNull(expiresAtStr);
        Assert.NotNull(serverTimeStr);

        var zRegex = new Regex(@"^\d{4}-\d{2}-\d{2}T.*Z$");
        Assert.Matches(zRegex, expiresAtStr);
        Assert.Matches(zRegex, serverTimeStr);

        var expiresAt = DateTime.Parse(expiresAtStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var serverTime = DateTime.Parse(serverTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind);

        Assert.Equal(DateTimeKind.Utc, expiresAt.Kind);
        Assert.Equal(DateTimeKind.Utc, serverTime.Kind);

        var diff = (expiresAt - serverTime).TotalSeconds;
        Assert.InRange(diff, 595, 605);
    }
}
