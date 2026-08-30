using Kheprx.BaseBackend.SharedKernel.Responses;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class ApiResponseTests
{
    [Fact]
    public void Success_sets_status_message_and_data()
    {
        var response = ApiResponse<int>.Success("ok", 5);
        Assert.True(response.SuccessStatus);
        Assert.Equal("ok", response.Message);
        Assert.Equal(5, response.Data);
        Assert.Null(response.Error);
    }

    [Fact]
    public void Failure_sets_status_false_and_error()
    {
        var response = ApiResponse<int>.Failure("bad", "CODE");
        Assert.False(response.SuccessStatus);
        Assert.Equal("bad", response.Message);
        Assert.Equal("CODE", response.Error);
    }
}
