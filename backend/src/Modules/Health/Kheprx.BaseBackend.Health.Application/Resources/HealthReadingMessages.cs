namespace Kheprx.BaseBackend.Health.Application.Resources;

/// <summary>Localized messages for health readings.</summary>
public static class HealthReadingMessages
{
    public static class Success
    {
        public static string Logged(string lang) => lang switch { "ar" => "تم تسجيل القراءة", _ => "Reading logged" };
        public static string Listed(string lang) => lang switch { "ar" => "قراءات السبّاح", _ => "Swimmer readings" };
        public static string Updated(string lang) => lang switch { "ar" => "تم تحديث القراءة", _ => "Reading updated" };
        public static string Deleted(string lang) => lang switch { "ar" => "تم حذف القراءة", _ => "Reading deleted" };
    }

    public static class Errors
    {
        public static string TestNotFound(string lang) => lang switch { "ar" => "الفحص الطبي غير موجود", _ => "Medical test not found" };
        public static string SwimmerRequired(string lang) => lang switch { "ar" => "السبّاح مطلوب", _ => "Swimmer is required" };
        public static string TestRequired(string lang) => lang switch { "ar" => "الفحص مطلوب", _ => "Test is required" };
        public static string NotFound(string lang) => lang switch { "ar" => "القراءة غير موجودة", _ => "Reading not found" };
        public static string ValuePositive(string lang) => lang switch { "ar" => "يجب أن تكون القيمة أكبر من صفر", _ => "Value must be greater than zero" };
    }
}
