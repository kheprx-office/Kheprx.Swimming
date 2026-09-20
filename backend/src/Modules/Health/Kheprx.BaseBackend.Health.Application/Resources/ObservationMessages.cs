namespace Kheprx.BaseBackend.Health.Application.Resources;

/// <summary>Localized messages for swimmer data fields (observations).</summary>
public static class ObservationMessages
{
    public static class Success
    {
        public static string Added(string lang) => lang switch { "ar" => "تم إضافة البيان", _ => "Data field added" };
        public static string Listed(string lang) => lang switch { "ar" => "بيانات السبّاح", _ => "Swimmer data fields" };
        public static string Updated(string lang) => lang switch { "ar" => "تم تحديث البيان", _ => "Data field updated" };
        public static string Deleted(string lang) => lang switch { "ar" => "تم حذف البيان", _ => "Data field deleted" };
    }

    public static class Errors
    {
        public static string SwimmerRequired(string lang) => lang switch { "ar" => "السبّاح مطلوب", _ => "Swimmer is required" };
        public static string CategoryRequired(string lang) => lang switch { "ar" => "الفئة مطلوبة", _ => "Category is required" };
        public static string FieldLabelRequired(string lang) => lang switch { "ar" => "اسم الحقل مطلوب", _ => "Field name is required" };
        public static string ValueRequired(string lang) => lang switch { "ar" => "القيمة مطلوبة", _ => "Value is required" };
        public static string NotFound(string lang) => lang switch { "ar" => "البيان غير موجود", _ => "Data field not found" };
    }
}
