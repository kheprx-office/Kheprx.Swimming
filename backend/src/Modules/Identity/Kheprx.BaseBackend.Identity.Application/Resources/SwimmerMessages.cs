namespace Kheprx.BaseBackend.Identity.Application.Resources;

/// <summary>Localized messages for swimmer queries.</summary>
public static class SwimmerMessages
{
    public static class Success
    {
        public static string CountRetrieved(string lang) => lang switch
        {
            "ar" => "عدد السبّاحين",
            _ => "Swimmer count"
        };

        public static string SwimmerCreated(string lang) => lang switch
        {
            "ar" => "تم تسجيل السبّاح بنجاح",
            _ => "Swimmer registered"
        };

        public static string SwimmersListed(string lang) => lang switch
        {
            "ar" => "قائمة السبّاحين",
            _ => "Swimmers list"
        };

        public static string ProfileRetrieved(string lang) => lang switch { "ar" => "بيانات السبّاح", _ => "Swimmer profile" };

        public static string IdentityUpdated(string lang) => lang switch { "ar" => "تم تحديث بيانات الهوية", _ => "Identity updated" };

        public static string ExamRecorded(string lang) => lang switch { "ar" => "تم تسجيل الفحص", _ => "Exam recorded" };

        public static string ExamsListed(string lang) => lang switch { "ar" => "سجل الفحوصات", _ => "Exam history" };
        public static string ExamUpdated(string lang) => lang switch { "ar" => "تم تحديث الفحص", _ => "Exam updated" };
        public static string ExamDeleted(string lang) => lang switch { "ar" => "تم حذف الفحص", _ => "Exam deleted" };
        public static string GuardiansRetrieved(string lang) => lang switch { "ar" => "بيانات ولي الأمر", _ => "Guardian details" };
        public static string GuardiansSaved(string lang) => lang switch { "ar" => "تم حفظ بيانات ولي الأمر", _ => "Guardian details saved" };
        public static string BodyMeasurementRetrieved(string lang) => lang switch { "ar" => "قياسات الجسم", _ => "Body measurements" };
        public static string BodyMeasurementSaved(string lang) => lang switch { "ar" => "تم حفظ قياسات الجسم", _ => "Body measurements saved" };
        public static string OnboardingPrefillRetrieved(string lang) => lang switch { "ar" => "بيانات البدء", _ => "Onboarding prefill" };
        public static string OnboardingCompleted(string lang) => lang switch { "ar" => "تم إكمال البيانات", _ => "Onboarding completed" };
    }

    public static class Errors
    {
        public static string UsernameTaken(string lang) => lang switch { "ar" => "اسم المستخدم مستخدم بالفعل", _ => "Username is already taken" };
        public static string EmailTaken(string lang) => lang switch { "ar" => "البريد الإلكتروني مستخدم بالفعل", _ => "Email is already in use" };
        public static string UnknownReference(string lang) => lang switch { "ar" => "قيمة مرجعية غير صالحة", _ => "One or more selected values are invalid" };
        public static string TrainingClubRequired(string lang) => lang switch { "ar" => "نادي التدريب مطلوب", _ => "Training club is required" };
        public static string GenderRequired(string lang) => lang switch { "ar" => "النوع مطلوب", _ => "Gender is required" };
        public static string DobRequired(string lang) => lang switch { "ar" => "تاريخ الميلاد مطلوب", _ => "Date of birth is required" };
        public static string DobInPast(string lang) => lang switch { "ar" => "يجب أن يكون تاريخ الميلاد في الماضي", _ => "Date of birth must be in the past" };
        public static string SpecializationRequired(string lang) => lang switch { "ar" => "اختر تخصصًا واحدًا على الأقل", _ => "Select at least one specialization" };
        public static string UsernameRequired(string lang) => lang switch { "ar" => "اسم المستخدم مطلوب", _ => "Username is required" };
        public static string ProfileNotFound(string lang) => lang switch { "ar" => "السبّاح غير موجود", _ => "Swimmer not found" };
        public static string MeasurementInvalid(string lang) => lang switch { "ar" => "قيمة قياس غير صالحة", _ => "A measurement value is invalid" };
        public static string AssessmentRequired(string lang) => lang switch { "ar" => "نتيجة التقييم مطلوبة", _ => "An assessment result is required" };
        public static string ExamDateInvalid(string lang) => lang switch { "ar" => "تاريخ الفحص غير صالح", _ => "The exam date is invalid" };
        public static string ExamNotFound(string lang) => lang switch { "ar" => "الفحص غير موجود", _ => "Exam not found" };
        public static string Forbidden(string lang) => lang switch { "ar" => "غير مصرح لك بعرض بيانات سبّاح آخر", _ => "You may only view your own data" };
    }
}
