namespace Kheprx.BaseBackend.Identity.Application.Resources;

/// <summary>Localized messages for auth operations (login/refresh/logout/me/change-password).</summary>
public static class AuthMessages
{
    public static class Success
    {
        public static string SignedIn(string lang) => lang switch
        {
            "ar" => "تم تسجيل الدخول بنجاح",
            _ => "Signed in"
        };

        public static string TokenRefreshed(string lang) => lang switch
        {
            "ar" => "تم تحديث الرمز بنجاح",
            _ => "Token refreshed"
        };

        public static string SignedOut(string lang) => lang switch
        {
            "ar" => "تم تسجيل الخروج بنجاح",
            _ => "Signed out"
        };

        public static string CurrentUser(string lang) => lang switch
        {
            "ar" => "المستخدم الحالي",
            _ => "Current user"
        };

        public static string PasswordChanged(string lang) => lang switch
        {
            "ar" => "تم تغيير كلمة المرور بنجاح",
            _ => "Password changed"
        };
    }

    public static class Errors
    {
        public static string InvalidCredentials(string lang) => lang switch
        {
            "ar" => "البريد الإلكتروني أو كلمة المرور غير صحيحة",
            _ => "Invalid email or password"
        };

        public static string InvalidRefreshToken(string lang) => lang switch
        {
            "ar" => "رمز التحديث غير صالح أو منتهي الصلاحية",
            _ => "Invalid or expired refresh token"
        };

        public static string NotAuthenticated(string lang) => lang switch
        {
            "ar" => "لم يتم تسجيل الدخول",
            _ => "Not authenticated"
        };

        public static string CurrentPasswordIncorrect(string lang) => lang switch
        {
            "ar" => "كلمة المرور الحالية غير صحيحة",
            _ => "Current password is incorrect"
        };

        public static string CurrentPasswordRequired(string lang) => lang switch
        {
            "ar" => "كلمة المرور الحالية مطلوبة",
            _ => "Current password is required"
        };

        public static string NewPasswordRequired(string lang) => lang switch
        {
            "ar" => "كلمة المرور الجديدة مطلوبة",
            _ => "New password is required"
        };

        public static string RefreshTokenRequired(string lang) => lang switch
        {
            "ar" => "رمز التحديث مطلوب",
            _ => "Refresh token is required"
        };
    }
}
