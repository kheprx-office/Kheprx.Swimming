namespace Kheprx.BaseBackend.Health.Application.Resources;

/// <summary>Localized messages for coach feedback entries.</summary>
public static class FeedbackMessages
{
    public static class Success
    {
        public static string Listed(string lang) => lang switch { "ar" => "التقييمات", _ => "Feedback" };
        public static string Created(string lang) => lang switch { "ar" => "تم حفظ التقييم", _ => "Feedback saved" };
        public static string Updated(string lang) => lang switch { "ar" => "تم تحديث التقييم", _ => "Feedback updated" };
        public static string Deleted(string lang) => lang switch { "ar" => "تم حذف التقييم", _ => "Feedback deleted" };
    }

    public static class Errors
    {
        public static string NotFound(string lang) => lang switch { "ar" => "التقييم غير موجود", _ => "Feedback not found" };
        public static string RatingInvalid(string lang) => lang switch { "ar" => "التقييم يجب أن يكون بين 1 و5", _ => "Rating must be between 1 and 5" };
        public static string CommentRequired(string lang) => lang switch { "ar" => "التعليق مطلوب", _ => "Comment is required" };
        public static string CategoryRequired(string lang) => lang switch { "ar" => "الفئة مطلوبة", _ => "Category is required" };
    }
}
