using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Tests;

public class ValidationAndEdgeCaseTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ValidationAndEdgeCaseTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string GenerateStudentId()
    {
        var random = new Random();
        return $"{random.Next(100000000, 999999999)}{random.Next(10000000, 99999999)}";
    }


    [Fact]
    public async Task Test4_Expiry_OneSecondHold_TwoSecondsWait_SeatFreed_ConfirmFails_GetHoldExpired()
    {
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("HOLD_DURATION_SECONDS", "1");
            builder.UseSetting("CLEANER_INTERVAL_SECONDS", "1");
        });
        var client = customFactory.CreateClient();

        var classRes = await client.PostAsJsonAsync("/classes", new CreateClassRequest("Expiry Test Class", 1));
        classRes.EnsureSuccessStatusCode();
        var cls = await classRes.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(cls);

        var student1 = GenerateStudentId();
        using var holdReq1 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
        holdReq1.Headers.Add(StudentIdValidator.HeaderName, student1);
        var holdRes1 = await client.SendAsync(holdReq1);
        Assert.Equal(HttpStatusCode.Created, holdRes1.StatusCode);
        var hold1 = await holdRes1.Content.ReadFromJsonAsync<CreateHoldResponse>();
        Assert.NotNull(hold1);

        var classResBefore = await client.GetAsync($"/classes/{cls.Id}");
        var clsBefore = await classResBefore.Content.ReadFromJsonAsync<ClassSummaryResponse>();
        Assert.NotNull(clsBefore);
        Assert.Equal(1, clsBefore.HeldSeats);
        Assert.Equal(0, clsBefore.AvailableSeats);

        await Task.Delay(2000);

        var classResAfter = await client.GetAsync($"/classes/{cls.Id}");
        var clsAfter = await classResAfter.Content.ReadFromJsonAsync<ClassSummaryResponse>();
        Assert.NotNull(clsAfter);
        Assert.Equal(0, clsAfter.HeldSeats);
        Assert.Equal(1, clsAfter.AvailableSeats);

        using var confirmReq = new HttpRequestMessage(HttpMethod.Post, $"/holds/{hold1.HoldId}/confirm");
        confirmReq.Headers.Add(StudentIdValidator.HeaderName, student1);
        var confirmRes = await client.SendAsync(confirmReq);
        Assert.Equal(HttpStatusCode.Conflict, confirmRes.StatusCode);
        var err = await confirmRes.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(err);
        Assert.Equal("hold_expired", err.Error);

        var getHoldRes = await client.GetAsync($"/holds/{hold1.HoldId}");
        Assert.Equal(HttpStatusCode.OK, getHoldRes.StatusCode);
        var holdDetails = await getHoldRes.Content.ReadFromJsonAsync<HoldDetailsResponse>();
        Assert.NotNull(holdDetails);
        Assert.Equal("EXPIRED", holdDetails.Status);

        var student2 = GenerateStudentId();
        using var holdReq2 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
        holdReq2.Headers.Add(StudentIdValidator.HeaderName, student2);
        var holdRes2 = await client.SendAsync(holdReq2);
        Assert.Equal(HttpStatusCode.Created, holdRes2.StatusCode);
    }

    [Fact]
    public async Task BackgroundService_CleansExpiredHoldsInDbAutomatically()
    {
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("HOLD_DURATION_SECONDS", "1");
            builder.UseSetting("CLEANER_INTERVAL_SECONDS", "1");
        });
        var client = customFactory.CreateClient();

        var classRes = await client.PostAsJsonAsync("/classes", new CreateClassRequest("Bg Cleaner Class", 5));
        var cls = await classRes.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(cls);

        var studentId = GenerateStudentId();
        using var holdReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
        holdReq.Headers.Add(StudentIdValidator.HeaderName, studentId);
        var holdRes = await client.SendAsync(holdReq);
        var hold = await holdRes.Content.ReadFromJsonAsync<CreateHoldResponse>();
        Assert.NotNull(hold);

        await Task.Delay(2500);

        using var scope = customFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var holdRow = await db.Holds.AsNoTracking().FirstOrDefaultAsync(h => h.Id == hold.HoldId);
        Assert.NotNull(holdRow);
        Assert.Equal("EXPIRED", holdRow.Status);
    }


    [Fact]
    public async Task Test5_BrokenJson_Returns400ValidationErrorNot500()
    {
        var brokenJson = "{\"name\": \"Bad JSON\", \"totalSeats\": ";
        using var content = new StringContent(brokenJson, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/classes", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("validation_error", error.Error);
    }

    [Fact]
    public async Task Test5_WrongTypes_Returns400ValidationErrorNot500()
    {
        var badPayload1 = "{\"name\": \"Class 1\", \"totalSeats\": \"twenty\"}";
        using var content1 = new StringContent(badPayload1, Encoding.UTF8, "application/json");
        var res1 = await _client.PostAsync("/classes", content1);
        Assert.Equal(HttpStatusCode.BadRequest, res1.StatusCode);
        var err1 = await res1.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(err1);
        Assert.Equal("validation_error", err1.Error);

        var badPayload2 = "{\"name\": 12345, \"totalSeats\": 10}";
        using var content2 = new StringContent(badPayload2, Encoding.UTF8, "application/json");
        var res2 = await _client.PostAsync("/classes", content2);
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);
        var err2 = await res2.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(err2);
        Assert.Equal("validation_error", err2.Error);
    }

    [Fact]
    public async Task Test5_MissingAndEmptyBody_Returns400ValidationErrorNot500()
    {
        using var emptyContent = new StringContent("", Encoding.UTF8, "application/json");
        var res1 = await _client.PostAsync("/classes", emptyContent);
        Assert.Equal(HttpStatusCode.BadRequest, res1.StatusCode);
        var err1 = await res1.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(err1);
        Assert.Equal("validation_error", err1.Error);

        using var emptyObjContent = new StringContent("{}", Encoding.UTF8, "application/json");
        var res2 = await _client.PostAsync("/classes", emptyObjContent);
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);
        var err2 = await res2.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(err2);
        Assert.Equal("validation_error", err2.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(100001)]
    public async Task Test5_OutOfRangeTotalSeats_Returns400ValidationErrorNot500(int invalidSeats)
    {
        var payload = JsonSerializer.Serialize(new { name = "Valid Name", totalSeats = invalidSeats });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/classes", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("validation_error", error.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Test5_EmptyOrWhitespaceName_Returns400ValidationErrorNot500(string invalidName)
    {
        var payload = JsonSerializer.Serialize(new { name = invalidName, totalSeats = 10 });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/classes", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("validation_error", error.Error);
    }

    [Fact]
    public async Task Test5_WrongId_NonExistentAndMalformedGuid_AllReturn404NotFoundNot500()
    {
        var nonExistentGuid = Guid.NewGuid();
        var studentId = GenerateStudentId();

        var res1 = await _client.GetAsync($"/classes/{nonExistentGuid}");
        Assert.Equal(HttpStatusCode.NotFound, res1.StatusCode);
        var err1 = await res1.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(err1);
        Assert.Equal("not_found", err1.Error);

        using (var holdReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{nonExistentGuid}/holds"))
        {
            holdReq.Headers.Add(StudentIdValidator.HeaderName, studentId);
            var res = await _client.SendAsync(holdReq);
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(err);
            Assert.Equal("not_found", err.Error);
        }

        var res2 = await _client.GetAsync($"/holds/{nonExistentGuid}");
        Assert.Equal(HttpStatusCode.NotFound, res2.StatusCode);
        var err2 = await res2.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(err2);
        Assert.Equal("not_found", err2.Error);

        using (var confReq = new HttpRequestMessage(HttpMethod.Post, $"/holds/{nonExistentGuid}/confirm"))
        {
            confReq.Headers.Add(StudentIdValidator.HeaderName, studentId);
            var res = await _client.SendAsync(confReq);
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(err);
            Assert.Equal("not_found", err.Error);
        }

        using (var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{nonExistentGuid}"))
        {
            delReq.Headers.Add(StudentIdValidator.HeaderName, studentId);
            var res = await _client.SendAsync(delReq);
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(err);
            Assert.Equal("not_found", err.Error);
        }

        var malformedRes1 = await _client.GetAsync("/classes/not-a-guid");
        Assert.Equal(HttpStatusCode.NotFound, malformedRes1.StatusCode);
        var malErr1 = await malformedRes1.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(malErr1);
        Assert.Equal("not_found", malErr1.Error);

        var malformedRes2 = await _client.GetAsync("/holds/not-a-guid");
        Assert.Equal(HttpStatusCode.NotFound, malformedRes2.StatusCode);
        var malErr2 = await malformedRes2.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(malErr2);
        Assert.Equal("not_found", malErr2.Error);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789012345678")]
    [InlineData("1234567890123456a")]
    [InlineData("à§§à§¨à§©à§ªà§«à§¬à§­à§®à§¯à§¦à§§à§¨à§©à§ªà§«à§¬à§­")]
    public async Task Test5_Headers_Malformed_AllReturn400InvalidStudentIdNot500(string badId)
    {
        var dummyId = Guid.NewGuid();

        using var req1 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{dummyId}/holds");
        req1.Headers.Add(StudentIdValidator.HeaderName, badId);
        var res1 = await _client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.BadRequest, res1.StatusCode);
        var err1 = await res1.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(err1);
        Assert.Equal("invalid_student_id", err1.Error);

        using var req2 = new HttpRequestMessage(HttpMethod.Get, "/me/holds");
        req2.Headers.Add(StudentIdValidator.HeaderName, badId);
        var res2 = await _client.SendAsync(req2);
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);
        var err2 = await res2.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(err2);
        Assert.Equal("invalid_student_id", err2.Error);
    }

    [Fact]
    public async Task Test5_Headers_Missing_AllReturn400InvalidStudentIdNot500()
    {
        var dummyId = Guid.NewGuid();

        var res1 = await _client.PostAsync($"/classes/{dummyId}/holds", null);
        Assert.Equal(HttpStatusCode.BadRequest, res1.StatusCode);
        var err1 = await res1.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(err1);
        Assert.Equal("invalid_student_id", err1.Error);

        var res2 = await _client.PostAsync($"/holds/{dummyId}/confirm", null);
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);
        var err2 = await res2.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(err2);
        Assert.Equal("invalid_student_id", err2.Error);

        var res3 = await _client.DeleteAsync($"/holds/{dummyId}");
        Assert.Equal(HttpStatusCode.BadRequest, res3.StatusCode);
        var err3 = await res3.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(err3);
        Assert.Equal("invalid_student_id", err3.Error);

        var res4 = await _client.GetAsync("/me/holds");
        Assert.Equal(HttpStatusCode.BadRequest, res4.StatusCode);
        var err4 = await res4.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(err4);
        Assert.Equal("invalid_student_id", err4.Error);
    }

    [Fact]
    public async Task Test5_UnsupportedMediaType_Returns415Or400ValidationErrorNot500()
    {
        using var content = new StringContent("some text", Encoding.UTF8, "text/plain");
        var response = await _client.PostAsync("/classes", content);

        Assert.True(response.StatusCode == HttpStatusCode.UnsupportedMediaType || response.StatusCode == HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("validation_error", error.Error);
    }

    [Fact]
    public async Task Test5_WrongMethodAndRoute_AllReturn4xxJsonNot500()
    {
        var res1 = await _client.PutAsync("/classes", null);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, res1.StatusCode);
        var err1 = await res1.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(err1);
        Assert.Equal("method_not_allowed", err1.Error);

        var res2 = await _client.GetAsync("/unknown/endpoint/path");
        Assert.Equal(HttpStatusCode.NotFound, res2.StatusCode);
        var err2 = await res2.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(err2);
        Assert.Equal("not_found", err2.Error);
    }
}
