using Microsoft.AspNetCore.Http;
using SchoolAdmission.Api.Common;

namespace SchoolAdmission.Tests;

public class StudentIdValidatorTests
{
    [Fact]
    public void ValidStudentId_ReturnsTrimmedString()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[StudentIdValidator.HeaderName] = "  12345678901234567  ";

        var result = StudentIdValidator.ExtractAndValidate(context);

        Assert.Equal("12345678901234567", result);
    }

    [Fact]
    public void MissingHeader_ThrowsInvalidStudentIdException()
    {
        var context = new DefaultHttpContext();

        var ex = Assert.Throws<InvalidStudentIdException>(() => StudentIdValidator.ExtractAndValidate(context));
        Assert.Equal("invalid_student_id", ex.ErrorCode);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void MultipleHeaderValues_ThrowsInvalidStudentIdException()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Append(StudentIdValidator.HeaderName, "12345678901234567");
        context.Request.Headers.Append(StudentIdValidator.HeaderName, "98765432109876543");

        var ex = Assert.Throws<InvalidStudentIdException>(() => StudentIdValidator.ExtractAndValidate(context));
        Assert.Equal("invalid_student_id", ex.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    [InlineData("123456789012345678")]
    [InlineData("1234567890123456a")]
    [InlineData("à§§à§¨à§©à§ªà§«à§¬à§­à§®à§¯à§¦à§§à§¨à§©à§ªà§«à§¬à§­")]
    public void InvalidStudentIdFormats_ThrowInvalidStudentIdException(string invalidId)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[StudentIdValidator.HeaderName] = invalidId;

        var ex = Assert.Throws<InvalidStudentIdException>(() => StudentIdValidator.ExtractAndValidate(context));
        Assert.Equal("invalid_student_id", ex.ErrorCode);
    }
}
