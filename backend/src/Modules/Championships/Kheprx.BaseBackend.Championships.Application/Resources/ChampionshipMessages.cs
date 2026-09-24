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

    public static class ScheduleSuccess
    {
        public static string Retrieved(string lang) => lang switch { "ar" => "الجدول", _ => "Schedule" };
        public static string Saved(string lang) => lang switch { "ar" => "تم حفظ الجدول", _ => "Schedule saved" };
    }

    public static class ScheduleErrors
    {
        public static string Invalid(string lang) => lang switch { "ar" => "بيانات الجدول غير صالحة (تأكد من العناوين، وأن لكل يوم تاريخاً مميزاً ضمن فترة البطولة، والسباقات، والسباحين المسجلين)", _ => "Invalid schedule (check day labels, that each day has a distinct date within the championship range, races, and that all swimmers are enrolled)" };
    }

    public static class NotFound
    {
        public static string Event(string lang) => lang switch { "ar" => "البطولة غير موجودة", _ => "Championship not found" };
    }

    public static class ResultsSuccess
    {
        public static string Retrieved(string lang) => lang switch { "ar" => "النتائج", _ => "Results" };
        public static string Saved(string lang) => lang switch { "ar" => "تم حفظ النتائج", _ => "Results saved" };
    }

    public static class ResultsErrors
    {
        public static string Invalid(string lang) => lang switch { "ar" => "بيانات النتائج غير صالحة (تأكد من الأوقات وأن كل سباح مُسند إلى هذا السباق)", _ => "Invalid results (check the times and that every swimmer is assigned to this race)" };
    }

    public static class SwimmerHistorySuccess
    {
        public static string Retrieved(string lang) => lang switch { "ar" => "سجل البطولات", _ => "Championship history" };
    }
}
