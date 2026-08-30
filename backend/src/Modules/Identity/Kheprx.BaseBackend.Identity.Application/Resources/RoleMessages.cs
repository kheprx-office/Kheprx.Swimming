namespace Kheprx.BaseBackend.Identity.Application.Resources;

/// <summary>Localized messages for role listing.</summary>
public static class RoleMessages
{
    public static class Success
    {
        public static string RolesListed(string lang) => lang switch
        {
            "ar" => "قائمة الأدوار",
            _ => "Roles"
        };
    }
}
