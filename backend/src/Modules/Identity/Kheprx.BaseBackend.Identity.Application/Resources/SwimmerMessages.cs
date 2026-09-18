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
        public static string BloodTypeRequired(string lang) => lang switch { "ar" => "فصيلة الدم مطلوبة", _ => "Blood type is required" };
        public static string SpecializationRequired(string lang) => lang switch { "ar" => "اختر تخصصًا واحدًا على الأقل", _ => "Select at least one specialization" };
        public static string UsernameRequired(string lang) => lang switch { "ar" => "اسم المستخدم مطلوب", _ => "Username is required" };
    }
}
