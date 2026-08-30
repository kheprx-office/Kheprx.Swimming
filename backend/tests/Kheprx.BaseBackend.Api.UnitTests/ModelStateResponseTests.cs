using Kheprx.BaseBackend.Api.Validation;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class ModelStateResponseTests
{
    [Fact]
    public void From_invalid_model_state_returns_failure_envelope()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("name", "The name field is required.");

        var response = ModelStateResponse.From(modelState);

        Assert.False(response.SuccessStatus);
        Assert.Equal(CommonMessages.Errors.ValidationFailed(AppLanguage.Current), response.Message);
        Assert.Contains("The name field is required.", response.Error);
    }

    [Fact]
    public void From_joins_multiple_error_messages()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("name", "Name required.");
        modelState.AddModelError("price", "Price invalid.");

        var response = ModelStateResponse.From(modelState);

        Assert.Contains("Name required.", response.Error);
        Assert.Contains("Price invalid.", response.Error);
    }

    [Fact]
    public void From_with_only_empty_messages_uses_fallback()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("name", string.Empty);

        var response = ModelStateResponse.From(modelState);

        Assert.False(response.SuccessStatus);
        Assert.Equal(CommonMessages.Errors.ValidationFailed(AppLanguage.Current), response.Message);
        Assert.Equal(CommonMessages.Errors.ValidationFallback(AppLanguage.Current), response.Error);
    }
}
