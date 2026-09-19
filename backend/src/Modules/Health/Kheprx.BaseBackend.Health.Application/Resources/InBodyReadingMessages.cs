namespace Kheprx.BaseBackend.Health.Application.Resources;

/// <summary>Localized messages for InBody readings.</summary>
public static class InBodyReadingMessages
{
    public static class Success
    {
        public static string Listed(string lang) => lang switch { "ar" => "قياسات InBody", _ => "InBody readings" };
        public static string Created(string lang) => lang switch { "ar" => "تم تسجيل القياس", _ => "Reading recorded" };
        public static string Updated(string lang) => lang switch { "ar" => "تم تحديث القياس", _ => "Reading updated" };
        public static string Deleted(string lang) => lang switch { "ar" => "تم حذف القياس", _ => "Reading deleted" };
    }

    public static class Errors
    {
        public static string NotFound(string lang) => lang switch { "ar" => "القياس غير موجود", _ => "Reading not found" };
        public static string DateRequired(string lang) => lang switch { "ar" => "تاريخ القياس مطلوب", _ => "Reading date is required" };
        public static string ValueInvalid(string lang) => lang switch { "ar" => "قيمة قياس غير صالحة", _ => "A measurement value is invalid" };
    }
}
