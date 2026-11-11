using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Tests;

public class ClassesEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ClassesEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostClass_WithValidData_Returns201Created()
    {
        var request = new CreateClassRequest("Class 6 Morning", 45);

        var response = await _client.PostAsJsonAsync("/classes", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var content = await response.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(content);
        Assert.NotEqual(Guid.Empty, content.Id);
        Assert.Equal("Class 6 Morning", content.Name);
        Assert.Equal(45, content.TotalSeats);
    }

    [Theory]
    [InlineData("", 50)]
    [InlineData("   ", 50)]
    [InlineData("Valid Name", null)]
    [InlineData("Valid Name", 0)]
    [InlineData("Valid Name", -10)]
    [InlineData("Valid Name", 100001)]
    public async Task PostClass_WithInvalidData_Returns400ValidationError(string? name, int? totalSeats)
    {
        var request = new CreateClassRequest(name, totalSeats);

        var response = await _client.PostAsJsonAsync("/classes", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("validation_error", error.Error);
    }

    [Fact]
    public async Task PostClass_WithNameExceeding200Chars_Returns400ValidationError()
    {
        var longName = new string('A', 201);
        var request = new CreateClassRequest(longName, 50);

        var response = await _client.PostAsJsonAsync("/classes", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("validation_error", error.Error);
    }

    [Fact]
    public async Task PostClass_WithMalformedJson_Returns400ValidationError()
    {
        var content = new StringContent("{ broken json ...", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/classes", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("validation_error", error.Error);
    }

    [Fact]
    public async Task GetClasses_Returns200WithSummaryArray()
    {
        var uniqueName = $"Class Test {Guid.NewGuid():N}";
        var postResponse = await _client.PostAsJsonAsync("/classes", new CreateClassRequest(uniqueName, 30));
        postResponse.EnsureSuccessStatusCode();

        var response = await _client.GetAsync("/classes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var classes = await response.Content.ReadFromJsonAsync<List<ClassSummaryResponse>>();
        Assert.NotNull(classes);

        var createdClass = classes.FirstOrDefault(c => c.Name == uniqueName);
        Assert.NotNull(createdClass);
        Assert.Equal(30, createdClass.TotalSeats);
        Assert.Equal(0, createdClass.HeldSeats);
        Assert.Equal(0, createdClass.ConfirmedSeats);
        Assert.Equal(30, createdClass.AvailableSeats);
    }

    [Fact]
    public async Task GetClassById_WithExistingId_Returns200()
    {
        var postResponse = await _client.PostAsJsonAsync("/classes", new CreateClassRequest("Class By Id Test", 25));
        postResponse.EnsureSuccessStatusCode();
        var created = await postResponse.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(created);

        var response = await _client.GetAsync($"/classes/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<ClassSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal(created.Id, summary.Id);
        Assert.Equal("Class By Id Test", summary.Name);
        Assert.Equal(25, summary.TotalSeats);
        Assert.Equal(25, summary.AvailableSeats);
    }

    [Fact]
    public async Task GetClassById_WithNonExistentGuid_Returns404NotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.GetAsync($"/classes/{nonExistentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("not_found", error.Error);
    }

    [Fact]
    public async Task GetClassById_WithInvalidGuidFormat_Returns404NotFound()
    {
        var response = await _client.GetAsync("/classes/not-a-valid-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("not_found", error.Error);
    }

    [Fact]
    public async Task UnknownRoute_Returns404JsonError()
    {
        var response = await _client.GetAsync("/api/completely-unknown-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("not_found", error.Error);
    }

    [Fact]
    public async Task MethodNotAllowed_Returns405JsonError()
    {
        var response = await _client.PutAsync("/classes", new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("method_not_allowed", error.Error);
    }
}
