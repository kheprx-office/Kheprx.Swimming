namespace Kheprx.BaseBackend.Identity.Application.Resources;

/// <summary>Localized messages for captain/head-coach registration.</summary>
public static class CoachMessages
{
    public static class Success
    {
        public static string CoachCreated(string lang) => lang switch { "ar" => "تم تسجيل الحساب بنجاح", _ => "Account registered" };
    }

    public static class Errors
    {
        public static string InvalidRole(string lang) => lang switch { "ar" => "النوع غير صالح", _ => "Invalid captain type" };
        public static string UsernameRequired(string lang) => lang switch { "ar" => "اسم المستخدم مطلوب", _ => "Username is required" };
        public static string GenderRequired(string lang) => lang switch { "ar" => "النوع مطلوب", _ => "Gender is required" };
        public static string DobRequired(string lang) => lang switch { "ar" => "تاريخ الميلاد مطلوب", _ => "Date of birth is required" };
        public static string DobInPast(string lang) => lang switch { "ar" => "يجب أن يكون تاريخ الميلاد في الماضي", _ => "Date of birth must be in the past" };
        public static string PhoneRequired(string lang) => lang switch { "ar" => "رقم الهاتف مطلوب", _ => "Phone is required" };
        public static string NationalIdInvalid(string lang) => lang switch { "ar" => "الرقم القومي يجب أن يكون ١٤ رقمًا", _ => "National ID must be 14 digits" };
        public static string UsernameTaken(string lang) => lang switch { "ar" => "اسم المستخدم مستخدم بالفعل", _ => "Username is already taken" };
        public static string EmailTaken(string lang) => lang switch { "ar" => "البريد الإلكتروني مستخدم بالفعل", _ => "Email is already in use" };
        public static string NationalIdTaken(string lang) => lang switch { "ar" => "الرقم القومي مستخدم بالفعل", _ => "National ID is already in use" };
    }
}
