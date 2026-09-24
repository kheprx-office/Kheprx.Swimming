namespace Kheprx.BaseBackend.Api.Resources;

public static class DashboardMessages
{
    public static string Loaded(string lang) => lang == "ar" ? "تم تحميل لوحة المعلومات." : "Dashboard loaded.";
}
