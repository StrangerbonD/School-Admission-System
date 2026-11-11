using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Tests;

public class HoldsEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public HoldsEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string GenerateStudentId()
    {
        var random = new Random();
        return $"{random.Next(100000000, 999999999)}{random.Next(10000000, 99999999)}";
    }

    private async Task<(Guid ClassId, Guid HoldId, string StudentId)> CreateClassAndHoldAsync(int totalSeats = 5)
    {
        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest($"Test Class {Guid.NewGuid():N}", totalSeats));
        classRes.EnsureSuccessStatusCode();
        var cls = await classRes.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(cls);

        var studentId = GenerateStudentId();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
        request.Headers.Add(StudentIdValidator.HeaderName, studentId);
        var holdRes = await _client.SendAsync(request);
        holdRes.EnsureSuccessStatusCode();
        var hold = await holdRes.Content.ReadFromJsonAsync<CreateHoldResponse>();
        Assert.NotNull(hold);

        return (cls.Id, hold.HoldId, studentId);
    }

    private async Task ExpireHoldInDbAsync(Guid holdId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE holds 
            SET expires_at = clock_timestamp() - interval '5 seconds' 
            WHERE id = {holdId}");
    }


    [Fact]
    public async Task PostHold_WithValidData_Returns201CreatedAndTimings()
    {
        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest("Hold Test Class 1", 10));
        classRes.EnsureSuccessStatusCode();
        var cls = await classRes.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(cls);

        var studentId = GenerateStudentId();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
        request.Headers.Add(StudentIdValidator.HeaderName, studentId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var rawJson = await response.Content.ReadAsStringAsync();
        var hold = await response.Content.ReadFromJsonAsync<CreateHoldResponse>();
        Assert.NotNull(hold);
        Assert.NotEqual(Guid.Empty, hold.HoldId);
        Assert.Equal(cls.Id, hold.ClassId);
        Assert.Equal(studentId, hold.StudentId);
        Assert.Equal("HELD", hold.Status);
        Assert.True(hold.ExpiresAt > hold.ServerTime);

        var zRegex = new Regex(@"^\d{4}-\d{2}-\d{2}T.*Z$");
        using var jsonDoc = JsonDocument.Parse(rawJson);
        var expiresAtStr = jsonDoc.RootElement.GetProperty("expiresAt").GetString();
        var serverTimeStr = jsonDoc.RootElement.GetProperty("serverTime").GetString();
        Assert.NotNull(expiresAtStr);
        Assert.NotNull(serverTimeStr);
        Assert.Matches(zRegex, expiresAtStr);
        Assert.Matches(zRegex, serverTimeStr);
    }

    [Fact]
    public async Task PostHold_WithoutStudentIdHeader_Returns400InvalidStudentId()
    {
        var classId = Guid.NewGuid();
        var response = await _client.PostAsync($"/classes/{classId}/holds", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("invalid_student_id", error.Error);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("123456789012345678")]
    [InlineData("1234567890123456a")]
    public async Task PostHold_WithMalformedStudentIdHeader_Returns400InvalidStudentId(string invalidId)
    {
        var classId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
        request.Headers.Add(StudentIdValidator.HeaderName, invalidId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("invalid_student_id", error.Error);
    }

    [Fact]
    public async Task PostHold_OnNonExistentClass_Returns404NotFound()
    {
        var nonExistentId = Guid.NewGuid();
        var studentId = GenerateStudentId();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{nonExistentId}/holds");
        request.Headers.Add(StudentIdValidator.HeaderName, studentId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("not_found", error.Error);
    }

    [Fact]
    public async Task PostHold_WhenAlreadyApplied_Returns409AlreadyApplied()
    {
        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest("Already Applied Class", 10));
        classRes.EnsureSuccessStatusCode();
        var cls = await classRes.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(cls);

        var studentId = GenerateStudentId();

        using (var req1 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds"))
        {
            req1.Headers.Add(StudentIdValidator.HeaderName, studentId);
            var res1 = await _client.SendAsync(req1);
            Assert.Equal(HttpStatusCode.Created, res1.StatusCode);
        }

        using (var req2 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds"))
        {
            req2.Headers.Add(StudentIdValidator.HeaderName, studentId);
            var res2 = await _client.SendAsync(req2);
            Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
            var error = await res2.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(error);
            Assert.Equal("already_applied", error.Error);
        }
    }

    [Fact]
    public async Task PostHold_WhenSeatsAreFull_Returns409NoSeats()
    {
        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest("1 Seat Class", 1));
        classRes.EnsureSuccessStatusCode();
        var cls = await classRes.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(cls);

        var student1 = GenerateStudentId();
        var student2 = GenerateStudentId();

        using (var req1 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds"))
        {
            req1.Headers.Add(StudentIdValidator.HeaderName, student1);
            var res1 = await _client.SendAsync(req1);
            Assert.Equal(HttpStatusCode.Created, res1.StatusCode);
        }

        using (var req2 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds"))
        {
            req2.Headers.Add(StudentIdValidator.HeaderName, student2);
            var res2 = await _client.SendAsync(req2);
            Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
            var error = await res2.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(error);
            Assert.Equal("no_seats", error.Error);
        }
    }


    [Fact]
    public async Task GetHoldById_WhenExists_ReturnsHoldDetailsAndTimingsWithoutAuthHeader()
    {
        var (classId, holdId, _) = await CreateClassAndHoldAsync();

        var response = await _client.GetAsync($"/holds/{holdId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var hold = await response.Content.ReadFromJsonAsync<HoldDetailsResponse>();
        Assert.NotNull(hold);
        Assert.Equal(holdId, hold.HoldId);
        Assert.Equal(classId, hold.ClassId);
        Assert.Equal("HELD", hold.Status);
        Assert.True(hold.ExpiresAt > hold.ServerTime);

        var rawJson = await response.Content.ReadAsStringAsync();
        var zRegex = new Regex(@"^\d{4}-\d{2}-\d{2}T.*Z$");
        using var jsonDoc = JsonDocument.Parse(rawJson);
        Assert.Matches(zRegex, jsonDoc.RootElement.GetProperty("expiresAt").GetString()!);
        Assert.Matches(zRegex, jsonDoc.RootElement.GetProperty("serverTime").GetString()!);
    }

    [Fact]
    public async Task GetHoldById_WhenNonExistent_Returns404NotFound()
    {
        var response = await _client.GetAsync($"/holds/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("not_found", error.Error);
    }

    [Fact]
    public async Task GetHoldById_WithInvalidGuid_Returns404NotFound()
    {
        var response = await _client.GetAsync("/holds/not-a-valid-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("not_found", error.Error);
    }

    [Fact]
    public async Task GetHoldById_WhenExpired_ReturnsComputedStatusExpired()
    {
        var (_, holdId, _) = await CreateClassAndHoldAsync();
        await ExpireHoldInDbAsync(holdId);

        var response = await _client.GetAsync($"/holds/{holdId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var hold = await response.Content.ReadFromJsonAsync<HoldDetailsResponse>();
        Assert.NotNull(hold);
        Assert.Equal("EXPIRED", hold.Status);
    }


    [Fact]
    public async Task ConfirmHold_WithValidHold_Returns200Confirmed()
    {
        var (classId, holdId, studentId) = await CreateClassAndHoldAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm");
        request.Headers.Add(StudentIdValidator.HeaderName, studentId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var confirmed = await response.Content.ReadFromJsonAsync<ConfirmHoldResponse>();
        Assert.NotNull(confirmed);
        Assert.Equal(holdId, confirmed.HoldId);
        Assert.Equal("CONFIRMED", confirmed.Status);

        var classRes = await _client.GetAsync($"/classes/{classId}");
        var cls = await classRes.Content.ReadFromJsonAsync<ClassSummaryResponse>();
        Assert.NotNull(cls);
        Assert.Equal(1, cls.ConfirmedSeats);
        Assert.Equal(0, cls.HeldSeats);
    }

    [Fact]
    public async Task ConfirmHold_MultipleTimesSequentially_IsIdempotentAndConfirmedSeatsCountDoesNotIncrease()
    {
        var (classId, holdId, studentId) = await CreateClassAndHoldAsync();

        for (int i = 0; i < 3; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm");
            request.Headers.Add(StudentIdValidator.HeaderName, studentId);

            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var confirmed = await response.Content.ReadFromJsonAsync<ConfirmHoldResponse>();
            Assert.NotNull(confirmed);
            Assert.Equal("CONFIRMED", confirmed.Status);
        }

        var classRes = await _client.GetAsync($"/classes/{classId}");
        var cls = await classRes.Content.ReadFromJsonAsync<ClassSummaryResponse>();
        Assert.NotNull(cls);
        Assert.Equal(1, cls.ConfirmedSeats);
    }

    [Fact]
    public async Task ConfirmHold_Parallel10Times_AllReturn200AndConfirmedSeatsIsExactlyOne()
    {
        var (classId, holdId, studentId) = await CreateClassAndHoldAsync();

        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm");
            req.Headers.Add(StudentIdValidator.HeaderName, studentId);
            return await _client.SendAsync(req);
        });

        var responses = await Task.WhenAll(tasks);

        foreach (var res in responses)
        {
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var body = await res.Content.ReadFromJsonAsync<ConfirmHoldResponse>();
            Assert.NotNull(body);
            Assert.Equal("CONFIRMED", body.Status);
        }

        var classRes = await _client.GetAsync($"/classes/{classId}");
        var cls = await classRes.Content.ReadFromJsonAsync<ClassSummaryResponse>();
        Assert.NotNull(cls);
        Assert.Equal(1, cls.ConfirmedSeats);
    }

    [Fact]
    public async Task ConfirmHold_ByAnotherStudent_Returns403Forbidden()
    {
        var (_, holdId, _) = await CreateClassAndHoldAsync();
        var anotherStudent = GenerateStudentId();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm");
        request.Headers.Add(StudentIdValidator.HeaderName, anotherStudent);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("forbidden", error.Error);
    }

    [Fact]
    public async Task ConfirmHold_WhenHoldCancelled_Returns409HoldCancelled()
    {
        var (_, holdId, studentId) = await CreateClassAndHoldAsync();

        using (var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{holdId}"))
        {
            delReq.Headers.Add(StudentIdValidator.HeaderName, studentId);
            var delRes = await _client.SendAsync(delReq);
            Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);
        }

        using (var confReq = new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm"))
        {
            confReq.Headers.Add(StudentIdValidator.HeaderName, studentId);
            var confRes = await _client.SendAsync(confReq);
            Assert.Equal(HttpStatusCode.Conflict, confRes.StatusCode);
            var error = await confRes.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(error);
            Assert.Equal("hold_cancelled", error.Error);
        }
    }

    [Fact]
    public async Task ConfirmHold_WhenHoldExpired_Returns409HoldExpired()
    {
        var (_, holdId, studentId) = await CreateClassAndHoldAsync();
        await ExpireHoldInDbAsync(holdId);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm");
        request.Headers.Add(StudentIdValidator.HeaderName, studentId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("hold_expired", error.Error);
    }

    [Fact]
    public async Task ConfirmHold_WhenHoldDoesNotExist_Returns404NotFound()
    {
        var nonExistentHold = Guid.NewGuid();
        var studentId = GenerateStudentId();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/holds/{nonExistentHold}/confirm");
        request.Headers.Add(StudentIdValidator.HeaderName, studentId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("not_found", error.Error);
    }

    [Fact]
    public async Task ConfirmHold_WithoutStudentIdHeader_Returns400InvalidStudentId()
    {
        var (_, holdId, _) = await CreateClassAndHoldAsync();

        var response = await _client.PostAsync($"/holds/{holdId}/confirm", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("invalid_student_id", error.Error);
    }


    [Fact]
    public async Task DeleteHold_WhenValid_Returns204NoContentAndFreesSeat()
    {
        var (classId, holdId, studentId) = await CreateClassAndHoldAsync(5);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{holdId}");
        request.Headers.Add(StudentIdValidator.HeaderName, studentId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var holdRes = await _client.GetAsync($"/holds/{holdId}");
        var hold = await holdRes.Content.ReadFromJsonAsync<HoldDetailsResponse>();
        Assert.NotNull(hold);
        Assert.Equal("CANCELLED", hold.Status);

        var classRes = await _client.GetAsync($"/classes/{classId}");
        var cls = await classRes.Content.ReadFromJsonAsync<ClassSummaryResponse>();
        Assert.NotNull(cls);
        Assert.Equal(0, cls.HeldSeats);
        Assert.Equal(5, cls.AvailableSeats);
    }

    [Fact]
    public async Task DeleteHold_MultipleTimesSequentially_IsIdempotentAndReturns204NoContent()
    {
        var (_, holdId, studentId) = await CreateClassAndHoldAsync();

        for (int i = 0; i < 3; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{holdId}");
            request.Headers.Add(StudentIdValidator.HeaderName, studentId);

            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    [Fact]
    public async Task DeleteHold_ByAnotherStudent_Returns403Forbidden()
    {
        var (_, holdId, _) = await CreateClassAndHoldAsync();
        var anotherStudent = GenerateStudentId();

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{holdId}");
        request.Headers.Add(StudentIdValidator.HeaderName, anotherStudent);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("forbidden", error.Error);
    }

    [Fact]
    public async Task DeleteHold_WhenAlreadyConfirmed_Returns409AlreadyConfirmed()
    {
        var (_, holdId, studentId) = await CreateClassAndHoldAsync();

        using (var confReq = new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm"))
        {
            confReq.Headers.Add(StudentIdValidator.HeaderName, studentId);
            var confRes = await _client.SendAsync(confReq);
            Assert.Equal(HttpStatusCode.OK, confRes.StatusCode);
        }

        using (var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{holdId}"))
        {
            delReq.Headers.Add(StudentIdValidator.HeaderName, studentId);
            var delRes = await _client.SendAsync(delReq);
            Assert.Equal(HttpStatusCode.Conflict, delRes.StatusCode);
            var error = await delRes.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(error);
            Assert.Equal("already_confirmed", error.Error);
        }
    }

    [Fact]
    public async Task DeleteHold_WhenExpired_Returns409HoldExpired()
    {
        var (_, holdId, studentId) = await CreateClassAndHoldAsync();
        await ExpireHoldInDbAsync(holdId);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{holdId}");
        request.Headers.Add(StudentIdValidator.HeaderName, studentId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("hold_expired", error.Error);
    }

    [Fact]
    public async Task DeleteHold_WhenHoldDoesNotExist_Returns404NotFound()
    {
        var nonExistentHold = Guid.NewGuid();
        var studentId = GenerateStudentId();

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{nonExistentHold}");
        request.Headers.Add(StudentIdValidator.HeaderName, studentId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("not_found", error.Error);
    }

    [Fact]
    public async Task DeleteHold_WithoutStudentIdHeader_Returns400InvalidStudentId()
    {
        var (_, holdId, _) = await CreateClassAndHoldAsync();

        var response = await _client.DeleteAsync($"/holds/{holdId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("invalid_student_id", error.Error);
    }


    [Fact]
    public async Task Reapply_AfterCancellation_Succeeds()
    {
        var (classId, holdId, studentId) = await CreateClassAndHoldAsync();

        using (var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{holdId}"))
        {
            delReq.Headers.Add(StudentIdValidator.HeaderName, studentId);
            var delRes = await _client.SendAsync(delReq);
            Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);
        }

        using (var reapplyReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds"))
        {
            reapplyReq.Headers.Add(StudentIdValidator.HeaderName, studentId);
            var reapplyRes = await _client.SendAsync(reapplyReq);
            Assert.Equal(HttpStatusCode.Created, reapplyRes.StatusCode);
            var newHold = await reapplyRes.Content.ReadFromJsonAsync<CreateHoldResponse>();
            Assert.NotNull(newHold);
            Assert.NotEqual(holdId, newHold.HoldId);
            Assert.Equal("HELD", newHold.Status);
        }
    }

    [Fact]
    public async Task Reapply_AfterExpiry_Succeeds()
    {
        var (classId, holdId, studentId) = await CreateClassAndHoldAsync();

        await ExpireHoldInDbAsync(holdId);

        using (var reapplyReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds"))
        {
            reapplyReq.Headers.Add(StudentIdValidator.HeaderName, studentId);
            var reapplyRes = await _client.SendAsync(reapplyReq);
            Assert.Equal(HttpStatusCode.Created, reapplyRes.StatusCode);
            var newHold = await reapplyRes.Content.ReadFromJsonAsync<CreateHoldResponse>();
            Assert.NotNull(newHold);
            Assert.NotEqual(holdId, newHold.HoldId);
            Assert.Equal("HELD", newHold.Status);
        }
    }


    [Fact]
    public async Task GetMyHolds_WithoutStudentIdHeader_Returns400InvalidStudentId()
    {
        var response = await _client.GetAsync("/me/holds");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("invalid_student_id", error.Error);
    }

    [Fact]
    public async Task GetMyHolds_WhenNoHolds_Returns200AndEmptyArray()
    {
        var studentId = GenerateStudentId();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/me/holds");
        request.Headers.Add(StudentIdValidator.HeaderName, studentId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<StudentHoldSummaryResponse>>();
        Assert.NotNull(list);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetMyHolds_ReturnsOnlyOwnHoldsSortedNewestFirstWithComputedStatus()
    {
        var studentA = GenerateStudentId();
        var studentB = GenerateStudentId();

        var classRes1 = await _client.PostAsJsonAsync("/classes", new CreateClassRequest("Class A1", 10));
        var cls1 = await classRes1.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(cls1);

        var classRes2 = await _client.PostAsJsonAsync("/classes", new CreateClassRequest("Class A2", 10));
        var cls2 = await classRes2.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(cls2);

        using (var req1 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls1.Id}/holds"))
        {
            req1.Headers.Add(StudentIdValidator.HeaderName, studentA);
            var res1 = await _client.SendAsync(req1);
            Assert.Equal(HttpStatusCode.Created, res1.StatusCode);
        }

        await Task.Delay(50);

        using (var req2 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls2.Id}/holds"))
        {
            req2.Headers.Add(StudentIdValidator.HeaderName, studentA);
            var res2 = await _client.SendAsync(req2);
            Assert.Equal(HttpStatusCode.Created, res2.StatusCode);
        }

        using (var reqB = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls1.Id}/holds"))
        {
            reqB.Headers.Add(StudentIdValidator.HeaderName, studentB);
            var resB = await _client.SendAsync(reqB);
            Assert.Equal(HttpStatusCode.Created, resB.StatusCode);
        }

        using var myHoldsReq = new HttpRequestMessage(HttpMethod.Get, "/me/holds");
        myHoldsReq.Headers.Add(StudentIdValidator.HeaderName, studentA);

        var response = await _client.SendAsync(myHoldsReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var holds = await response.Content.ReadFromJsonAsync<List<StudentHoldSummaryResponse>>();
        Assert.NotNull(holds);
        Assert.Equal(2, holds.Count);

        Assert.Equal(cls2.Id, holds[0].ClassId);
        Assert.Equal("Class A2", holds[0].ClassName);
        Assert.Equal("HELD", holds[0].Status);

        Assert.Equal(cls1.Id, holds[1].ClassId);
        Assert.Equal("Class A1", holds[1].ClassName);
        Assert.Equal("HELD", holds[1].Status);

        var zRegex = new Regex(@"^\d{4}-\d{2}-\d{2}T.*Z$");
        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            Assert.Matches(zRegex, item.GetProperty("expiresAt").GetString()!);
            Assert.Matches(zRegex, item.GetProperty("serverTime").GetString()!);
        }
    }


    [Fact]
    public async Task ConcurrentConfirmAndCancel_ExactlyOneWinsAndTheOtherReceives409()
    {
        var (_, holdId, studentId) = await CreateClassAndHoldAsync();

        var confirmTask = Task.Run(async () =>
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm");
            req.Headers.Add(StudentIdValidator.HeaderName, studentId);
            return await _client.SendAsync(req);
        });

        var cancelTask = Task.Run(async () =>
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{holdId}");
            req.Headers.Add(StudentIdValidator.HeaderName, studentId);
            return await _client.SendAsync(req);
        });

        var results = await Task.WhenAll(confirmTask, cancelTask);
        var confirmRes = results[0];
        var cancelRes = results[1];

        if (confirmRes.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal(HttpStatusCode.Conflict, cancelRes.StatusCode);
            var err = await cancelRes.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(err);
            Assert.Equal("already_confirmed", err.Error);
        }
        else
        {
            Assert.Equal(HttpStatusCode.NoContent, cancelRes.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, confirmRes.StatusCode);
            var err = await confirmRes.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(err);
            Assert.Equal("hold_cancelled", err.Error);
        }
    }
}
