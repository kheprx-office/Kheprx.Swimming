using Kheprx.BaseBackend.SharedKernel.Responses;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Kheprx.BaseBackend.Api.Validation;

public static class ModelStateResponse
{
    public static ApiResponse<object> From(ModelStateDictionary modelState)
    {
        var errors = string.Join("; ", modelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message)));

        if (string.IsNullOrWhiteSpace(errors))
            errors = CommonMessages.Errors.ValidationFallback(AppLanguage.Current);

        return ApiResponse<object>.Failure(CommonMessages.Errors.ValidationFailed(AppLanguage.Current), errors);
    }
}
