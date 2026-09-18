using System.Globalization;
using Kheprx.BaseBackend.SharedKernel.Resources;
using Xunit;

namespace Kheprx.BaseBackend.Api.UnitTests;

public class AppLanguageTests
{
    [Theory]
    [InlineData("ar", "ar")]
    [InlineData("ar-EG", "ar")]
    [InlineData("en", "en")]
    [InlineData("en-US", "en")]
    public void Current_reflects_the_current_ui_culture(string culture, string expected)
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            Assert.Equal(expected, AppLanguage.Current);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
