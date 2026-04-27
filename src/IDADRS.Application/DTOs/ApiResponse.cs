namespace IDADRS.Application.DTOs;

/// <summary>Uniform API envelope returned by every controller action.</summary>
public sealed class ApiResponse<T>
{
    public bool   Success { get; init; }
    public T?     Data    { get; init; }
    public string Message { get; init; } = string.Empty;

    public static ApiResponse<T> Ok(T data, string message = "Success") =>
        new() { Success = true,  Data = data,    Message = message };

    public static ApiResponse<T> Fail(string message) =>
        new() { Success = false, Data = default, Message = message };
}

/// <summary>Non-generic convenience overload for void responses.</summary>
public static class ApiResponse
{
    public static ApiResponse<object?> Ok(string message = "Success") =>
        ApiResponse<object?>.Ok(null, message);

    public static ApiResponse<object?> Fail(string message) =>
        ApiResponse<object?>.Fail(message);
}
