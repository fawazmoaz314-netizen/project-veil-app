using Microsoft.AspNetCore.Mvc;

namespace Veil.Api.Common;

public enum ServiceErrorKind
{
    Validation,
    NotFound,
    Unauthorized,
    Conflict,
    RateLimited,
}

public sealed record ServiceError(ServiceErrorKind Kind, string Message)
{
    public static ServiceError Validation(string message) => new(ServiceErrorKind.Validation, message);
    public static ServiceError NotFound(string message) => new(ServiceErrorKind.NotFound, message);
    public static ServiceError Unauthorized(string message) => new(ServiceErrorKind.Unauthorized, message);
    public static ServiceError Conflict(string message) => new(ServiceErrorKind.Conflict, message);
    public static ServiceError RateLimited(string message) => new(ServiceErrorKind.RateLimited, message);

    public int StatusCode => Kind switch
    {
        ServiceErrorKind.Validation => StatusCodes.Status400BadRequest,
        ServiceErrorKind.NotFound => StatusCodes.Status404NotFound,
        ServiceErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ServiceErrorKind.Conflict => StatusCodes.Status409Conflict,
        ServiceErrorKind.RateLimited => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status500InternalServerError,
    };
}

public readonly struct ServiceResult<T>
{
    private ServiceResult(T? value, ServiceError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public ServiceError? Error { get; }
    public bool IsSuccess => Error is null;

    public static implicit operator ServiceResult<T>(T value) => new(value, null);
    public static implicit operator ServiceResult<T>(ServiceError error) => new(default, error);
}

public static class ServiceResultExtensions
{
    public static ObjectResult ToProblem(this ControllerBase controller, ServiceError error) =>
        controller.Problem(detail: error.Message, statusCode: error.StatusCode);
}
