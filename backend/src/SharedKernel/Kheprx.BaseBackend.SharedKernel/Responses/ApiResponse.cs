namespace Kheprx.BaseBackend.SharedKernel.Responses;

public sealed class ApiResponse<T> : BaseApiResponse
{
    public T? Data { get; init; }

    public static ApiResponse<T> Success(string message, T? data) =>
        new() { SuccessStatus = true, Message = message, Data = data };

    public static ApiResponse<T> Failure(string message, string error) =>
        new() { SuccessStatus = false, Message = message, Error = error };
}
