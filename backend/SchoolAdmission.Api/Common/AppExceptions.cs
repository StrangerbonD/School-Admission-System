namespace SchoolAdmission.Api.Common;

public class AppException : Exception
{
    public string ErrorCode { get; }
    public int StatusCode { get; }

    public AppException(string errorCode, string message, int statusCode) : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}

public class ValidationException : AppException
{
    public ValidationException(string message) : base("validation_error", message, 400) { }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base("not_found", message, 404) { }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "You do not have permission to access this resource.")
        : base("forbidden", message, 403) { }
}

public class ConflictException : AppException
{
    public ConflictException(string errorCode, string message) : base(errorCode, message, 409) { }
}

public class InvalidStudentIdException : AppException
{
    public InvalidStudentIdException(string message = "Invalid student ID.")
        : base("invalid_student_id", message, 400) { }
}
