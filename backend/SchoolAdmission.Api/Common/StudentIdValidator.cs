using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace SchoolAdmission.Api.Common;

public static partial class StudentIdValidator
{
    [GeneratedRegex("^[0-9]{17}$")]
    private static partial Regex StudentIdRegex();

    public const string HeaderName = "X-Student-Id";

    public static string ExtractAndValidate(HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            throw new InvalidStudentIdException("Missing X-Student-Id header.");
        }

        if (values.Count != 1)
        {
            throw new InvalidStudentIdException("Multiple X-Student-Id header values provided.");
        }

        var raw = values[0];
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidStudentIdException("Empty X-Student-Id header.");
        }

        var trimmed = raw.Trim();
        if (trimmed.Length != 17 || !StudentIdRegex().IsMatch(trimmed))
        {
            throw new InvalidStudentIdException("X-Student-Id must be exactly 17 digits [0-9].");
        }

        return trimmed;
    }
}
