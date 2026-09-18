namespace Kheprx.BaseBackend.Health.Application.Resources;

/// <summary>Localized messages for health readings.</summary>
public static class HealthReadingMessages
{
    public static class Success
    {
        public static string Logged(string lang) => lang switch { "ar" => "تم تسجيل القراءة", _ => "Reading logged" };
    }

    public static class Errors
    {
        public static string TestNotFound(string lang) => lang switch { "ar" => "الفحص الطبي غير موجود", _ => "Medical test not found" };
        public static string SwimmerRequired(string lang) => lang switch { "ar" => "السبّاح مطلوب", _ => "Swimmer is required" };
        public static string TestRequired(string lang) => lang switch { "ar" => "الفحص مطلوب", _ => "Test is required" };
    }
}
