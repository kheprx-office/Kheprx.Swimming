namespace Kheprx.BaseBackend.Championships.Application.Resources;

/// <summary>Localized messages for championship queries.</summary>
public static class ChampionshipMessages
{
    public static class Success
    {
        public static string Listed(string lang) => lang switch { "ar" => "البطولات", _ => "Championships" };
        public static string Created(string lang) => lang switch { "ar" => "تم إنشاء البطولة", _ => "Championship created" };
    }

    public static class Errors
    {
        public static string NameRequired(string lang) => lang switch { "ar" => "اسم البطولة مطلوب", _ => "Championship name is required" };
        public static string LocationRequired(string lang) => lang switch { "ar" => "الموقع مطلوب", _ => "Location is required" };
        public static string EndBeforeStart(string lang) => lang switch { "ar" => "تاريخ الانتهاء لا يمكن أن يسبق تاريخ البدء", _ => "End date cannot be before the start date" };
        public static string StatusUnavailable(string lang) => lang switch { "ar" => "حالة \"قادمة\" غير متوفرة", _ => "The 'upcoming' status is unavailable" };
    }

    public static class EnrollmentSuccess
    {
        public static string Retrieved(string lang) => lang switch { "ar" => "التسجيلات", _ => "Enrollments" };
        public static string Saved(string lang) => lang switch { "ar" => "تم حفظ التسجيلات", _ => "Enrollments saved" };
    }

    public static class NotFound
    {
        public static string Event(string lang) => lang switch { "ar" => "البطولة غير موجودة", _ => "Championship not found" };
    }
}
