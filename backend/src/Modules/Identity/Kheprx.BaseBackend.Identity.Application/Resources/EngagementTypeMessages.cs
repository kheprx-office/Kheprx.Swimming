namespace Kheprx.BaseBackend.Identity.Application.Resources;

/// <summary>Localized messages for engagement-type lookups.</summary>
public static class EngagementTypeMessages
{
    public static class Success
    {
        public static string EngagementTypesListed(string lang) => lang switch
        {
            "ar" => "قائمة أنواع التعاقد",
            _ => "Engagement types"
        };
    }
}
