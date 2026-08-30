using Kheprx.BaseBackend.Identity.Application.Resources;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Resources;

public class IdentityMessagesTests
{
    [Fact]
    public void AuthMessages_InvalidCredentials_switches_on_language()
    {
        Assert.Equal("البريد الإلكتروني أو كلمة المرور غير صحيحة", AuthMessages.Errors.InvalidCredentials("ar"));
        Assert.Equal("Invalid email or password", AuthMessages.Errors.InvalidCredentials("en"));
    }

    [Fact]
    public void UserMessages_EmailInUse_switches_on_language()
    {
        Assert.Equal("البريد الإلكتروني مستخدم بالفعل", UserMessages.Errors.EmailInUse("ar"));
        Assert.Equal("Email is already in use", UserMessages.Errors.EmailInUse("en"));
    }

    [Fact]
    public void RoleMessages_RolesListed_switches_on_language()
    {
        Assert.Equal("قائمة الأدوار", RoleMessages.Success.RolesListed("ar"));
        Assert.Equal("Roles", RoleMessages.Success.RolesListed("en"));
    }

    [Fact]
    public void UserMessages_NidInUse_switches_on_language()
    {
        Assert.Equal("الرقم القومي مستخدم بالفعل", UserMessages.Errors.NidInUse("ar"));
        Assert.Equal("National ID is already in use", UserMessages.Errors.NidInUse("en"));
    }

    [Fact]
    public void EngagementTypeMessages_Listed_switches_on_language()
    {
        Assert.Equal("قائمة أنواع التعاقد", EngagementTypeMessages.Success.EngagementTypesListed("ar"));
        Assert.Equal("Engagement types", EngagementTypeMessages.Success.EngagementTypesListed("en"));
    }
}
