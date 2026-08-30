namespace Kheprx.BaseBackend.SharedKernel.Resources;

/// <summary>Localized messages shared across modules: validation-generic field texts and server errors.</summary>
public static class CommonMessages
{
    public static class Errors
    {
        public static string ValidationFailed(string lang) => lang switch
        {
            "ar" => "فشل التحقق من البيانات",
            _ => "Validation failed"
        };

        public static string ValidationFallback(string lang) => lang switch
        {
            "ar" => "حدث خطأ في التحقق من البيانات",
            _ => "One or more validation errors occurred."
        };

        public static string ServerError(string lang) => lang switch
        {
            "ar" => "حدث خطأ غير متوقع",
            _ => "An unexpected error occurred."
        };

        public static string EmailRequired(string lang) => lang switch
        {
            "ar" => "البريد الإلكتروني مطلوب",
            _ => "Email is required"
        };

        public static string EmailInvalid(string lang) => lang switch
        {
            "ar" => "صيغة البريد الإلكتروني غير صحيحة",
            _ => "Email format is invalid"
        };

        public static string EmailTooLong(string lang) => lang switch
        {
            "ar" => "البريد الإلكتروني طويل جدًا",
            _ => "Email is too long"
        };

        public static string PasswordRequired(string lang) => lang switch
        {
            "ar" => "كلمة المرور مطلوبة",
            _ => "Password is required"
        };

        public static string PasswordMinLength(string lang) => lang switch
        {
            "ar" => "كلمة المرور يجب ألا تقل عن 8 أحرف",
            _ => "Password must be at least 8 characters"
        };

        public static string PasswordTooLong(string lang) => lang switch
        {
            "ar" => "كلمة المرور طويلة جدًا",
            _ => "Password is too long"
        };

        public static string FullNameRequired(string lang) => lang switch
        {
            "ar" => "الاسم الكامل مطلوب",
            _ => "Full name is required"
        };

        public static string FullNameTooLong(string lang) => lang switch
        {
            "ar" => "الاسم الكامل طويل جدًا",
            _ => "Full name is too long"
        };
    }
}
