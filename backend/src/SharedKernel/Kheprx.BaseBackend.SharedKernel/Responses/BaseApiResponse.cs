namespace Kheprx.BaseBackend.SharedKernel.Responses;

public abstract class BaseApiResponse
{
    public bool SuccessStatus { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Error { get; init; }
}
