namespace Kheprx.BaseBackend.Health.Application.Resources;

/// <summary>Localized messages for the medical-test catalog.</summary>
public static class MedicalTestMessages
{
    public static class Success
    {
        public static string Listed(string lang) => lang switch { "ar" => "قائمة الفحوصات الطبية", _ => "Medical tests" };
        public static string Created(string lang) => lang switch { "ar" => "تمت إضافة الفحص الطبي", _ => "Medical test added" };
        public static string Deleted(string lang) => lang switch { "ar" => "تم حذف الفحص الطبي", _ => "Medical test deleted" };
    }

    public static class Errors
    {
        public static string NotFound(string lang) => lang switch { "ar" => "الفحص الطبي غير موجود", _ => "Medical test not found" };
        public static string NameEnRequired(string lang) => lang switch { "ar" => "اسم الفحص (بالإنجليزية) مطلوب", _ => "Test name (EN) is required" };
        public static string NameArRequired(string lang) => lang switch { "ar" => "اسم الفحص (بالعربية) مطلوب", _ => "Test name (AR) is required" };
        public static string UnitRequired(string lang) => lang switch { "ar" => "الوحدة مطلوبة", _ => "Unit is required" };
        public static string UpperMustExceedLower(string lang) => lang switch { "ar" => "يجب أن يكون الحد الأعلى أكبر من الحد الأدنى", _ => "Upper bound must exceed lower bound" };
    }
}
