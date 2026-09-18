using Kheprx.BaseBackend.SharedKernel.Resources;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class CommonMessagesTests
{
    // AppLanguage.Current resolution (from the request culture) is covered by AppLanguageTests.

    [Fact]
    public void ValidationFailed_switches_on_language()
    {
        Assert.Equal("فشل التحقق من البيانات", CommonMessages.Errors.ValidationFailed("ar"));
        Assert.Equal("Validation failed", CommonMessages.Errors.ValidationFailed("en"));
    }

    [Fact]
    public void ServerError_switches_on_language()
    {
        Assert.Equal("حدث خطأ غير متوقع", CommonMessages.Errors.ServerError("ar"));
        Assert.Equal("An unexpected error occurred.", CommonMessages.Errors.ServerError("en"));
    }
}
