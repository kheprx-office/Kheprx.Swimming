namespace Kheprx.BaseBackend.Identity.Application.Resources;

/// <summary>Localized messages for user-management operations.</summary>
public static class UserMessages
{
    public static class Success
    {
        public static string UsersListed(string lang) => lang switch
        {
            "ar" => "قائمة المستخدمين",
            _ => "Users"
        };

        public static string UserCreated(string lang) => lang switch
        {
            "ar" => "تم إنشاء المستخدم بنجاح",
            _ => "User created"
        };

        public static string UserUpdated(string lang) => lang switch
        {
            "ar" => "تم تحديث المستخدم بنجاح",
            _ => "User updated"
        };

        public static string StatusUpdated(string lang) => lang switch
        {
            "ar" => "تم تحديث الحالة بنجاح",
            _ => "Status updated"
        };
    }

    public static class Errors
    {
        public static string EmailInUse(string lang) => lang switch
        {
            "ar" => "البريد الإلكتروني مستخدم بالفعل",
            _ => "Email is already in use"
        };

        public static string UserNotFound(string lang) => lang switch
        {
            "ar" => "المستخدم غير موجود",
            _ => "User not found"
        };

        public static string RoleRequired(string lang) => lang switch
        {
            "ar" => "الدور مطلوب",
            _ => "Role is required"
        };

        public static string UnknownRole(string lang) => lang switch
        {
            "ar" => "الدور غير معروف",
            _ => "Unknown role."
        };

        public static string StatusRequired(string lang) => lang switch
        {
            "ar" => "الحالة مطلوبة",
            _ => "Status is required"
        };

        public static string InvalidStatus(string lang) => lang switch
        {
            "ar" => "الحالة غير صالحة",
            _ => "Invalid status."
        };

        public static string NidRequired(string lang) => lang switch
        {
            "ar" => "الرقم القومي مطلوب",
            _ => "National ID is required"
        };

        public static string NidInvalid(string lang) => lang switch
        {
            "ar" => "الرقم القومي يجب أن يتكون من 14 رقمًا",
            _ => "National ID must be exactly 14 digits"
        };

        public static string NidInUse(string lang) => lang switch
        {
            "ar" => "الرقم القومي مستخدم بالفعل",
            _ => "National ID is already in use"
        };

        public static string InvalidGender(string lang) => lang switch
        {
            "ar" => "الجنس غير صالح",
            _ => "Invalid gender."
        };

        public static string InvalidAge(string lang) => lang switch
        {
            "ar" => "العمر يجب أن يكون بين 14 و90",
            _ => "Age must be between 14 and 90"
        };

        public static string PhoneRequired(string lang) => lang switch
        {
            "ar" => "رقم الهاتف مطلوب",
            _ => "Phone number is required"
        };

        public static string PhoneInvalid(string lang) => lang switch
        {
            "ar" => "رقم هاتف غير صالح",
            _ => "Invalid phone number"
        };

        public static string GenderRequired(string lang) => lang switch
        {
            "ar" => "الجنس مطلوب",
            _ => "Gender is required"
        };

        public static string AgeRequired(string lang) => lang switch
        {
            "ar" => "العمر مطلوب",
            _ => "Age is required"
        };

        public static string MonthlySalaryRequired(string lang) => lang switch
        {
            "ar" => "الراتب الشهري مطلوب",
            _ => "Monthly salary is required"
        };

        public static string MonthlySalaryInvalid(string lang) => lang switch
        {
            "ar" => "الراتب الشهري غير صالح",
            _ => "Monthly salary is invalid"
        };

        public static string DailyWageRequired(string lang) => lang switch
        {
            "ar" => "اليومية مطلوبة",
            _ => "Daily wage is required"
        };

        public static string DailyWageInvalid(string lang) => lang switch
        {
            "ar" => "اليومية غير صالحة",
            _ => "Daily wage is invalid"
        };

        public static string EngagementTypeRequired(string lang) => lang switch
        {
            "ar" => "نوع التعاقد مطلوب",
            _ => "Engagement type is required"
        };

        public static string UnknownEngagementType(string lang) => lang switch
        {
            "ar" => "نوع التعاقد غير معروف",
            _ => "Unknown engagement type."
        };

        public static string FieldNotAllowedForRole(string lang) => lang switch
        {
            "ar" => "حقل غير مسموح به لهذا الدور",
            _ => "Field not allowed for this role."
        };

        public static string RoleImmutable(string lang) => lang switch
        {
            "ar" => "لا يمكن تغيير الدور",
            _ => "Role cannot be changed."
        };
    }
}
