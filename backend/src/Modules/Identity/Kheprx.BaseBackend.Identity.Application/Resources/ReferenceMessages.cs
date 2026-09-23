namespace Kheprx.BaseBackend.Identity.Application.Resources;

/// <summary>Localized success messages for reference lookup queries.</summary>
public static class ReferenceMessages
{
    public static class Success
    {
        public static string ClubsListed(string lang) => lang switch { "ar" => "قائمة الأندية", _ => "Clubs" };
        public static string BloodTypesListed(string lang) => lang switch { "ar" => "فصائل الدم", _ => "Blood types" };
        public static string StrokesListed(string lang) => lang switch { "ar" => "أنواع السباحة", _ => "Strokes" };
        public static string GendersListed(string lang) => lang switch { "ar" => "الأنواع", _ => "Genders" };
        public static string ObservationCategoriesListed(string lang) => lang switch { "ar" => "فئات البيانات", _ => "Observation categories" };
        public static string FitnessAssessmentsListed(string lang) => lang switch { "ar" => "تقييمات اللياقة", _ => "Fitness assessments" };
        public static string FeedbackCategoriesListed(string lang) => lang switch { "ar" => "فئات التقييم", _ => "Feedback categories" };
        public static string AttendanceStatusesListed(string lang) => lang switch { "ar" => "حالات الحضور", _ => "Attendance statuses" };
        public static string CompetitionStatusesListed(string lang) => lang switch { "ar" => "حالات البطولات", _ => "Competition statuses" };
    }
}
