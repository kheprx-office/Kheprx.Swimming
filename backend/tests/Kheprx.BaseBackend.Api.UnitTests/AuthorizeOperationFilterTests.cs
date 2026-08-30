using System.Text.Json;
using Kheprx.BaseBackend.Api.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class AuthorizeOperationFilterTests
{
    [Authorize]
    private sealed class ProtectedController
    {
        public void Action() { }
        [AllowAnonymous] public void Anon() { }
    }

    [Authorize(Roles = "admin")]
    private sealed class AdminController
    {
        public void Action() { }
    }

    private sealed class OpenController
    {
        public void Plain() { }
        [Authorize] public void Locked() { }
    }

    private static OpenApiOperation Apply(Type controllerType, string methodName, OpenApiOperation? operation = null)
    {
        operation ??= new OpenApiOperation();
        var context = new OperationFilterContext(
            new ApiDescription(),
            new SchemaGenerator(new SchemaGeneratorOptions(),
                new JsonSerializerDataContractResolver(new JsonSerializerOptions())),
            new SchemaRepository(),
            controllerType.GetMethod(methodName)!);
        new AuthorizeOperationFilter().Apply(operation, context);
        return operation;
    }

    [Fact]
    public void Controller_level_authorize_adds_security_requirement_and_401()
    {
        var operation = Apply(typeof(ProtectedController), nameof(ProtectedController.Action));

        Assert.Single(operation.Security);
        Assert.Equal("Bearer", operation.Security.Single().Keys.Single().Reference.Id);
        Assert.True(operation.Responses.ContainsKey("401"));
        Assert.False(operation.Responses.ContainsKey("403"));
    }

    [Fact]
    public void Role_restricted_authorize_adds_403()
    {
        var operation = Apply(typeof(AdminController), nameof(AdminController.Action));

        Assert.True(operation.Responses.ContainsKey("401"));
        Assert.True(operation.Responses.ContainsKey("403"));
    }

    [Fact]
    public void Action_level_authorize_adds_security_requirement_and_401()
    {
        var operation = Apply(typeof(OpenController), nameof(OpenController.Locked));

        Assert.Single(operation.Security);
        Assert.True(operation.Responses.ContainsKey("401"));
    }

    [Fact]
    public void Unattributed_action_is_untouched()
    {
        var operation = Apply(typeof(OpenController), nameof(OpenController.Plain));

        Assert.Empty(operation.Security);
        Assert.Empty(operation.Responses);
    }

    [Fact]
    public void Allow_anonymous_action_on_protected_controller_is_untouched()
    {
        var operation = Apply(typeof(ProtectedController), nameof(ProtectedController.Anon));

        Assert.Empty(operation.Security);
        Assert.Empty(operation.Responses);
    }

    [Fact]
    public void Existing_401_response_is_not_overwritten()
    {
        var operation = new OpenApiOperation();
        operation.Responses["401"] = new OpenApiResponse { Description = "custom" };

        Apply(typeof(ProtectedController), nameof(ProtectedController.Action), operation);

        Assert.Equal("custom", operation.Responses["401"].Description);
        Assert.Single(operation.Security); // security requirement is still attached
    }
}
