namespace Kheprx.BaseBackend.Attendance.Application.Resources;

/// <summary>Localized messages for attendance queries.</summary>
public static class AttendanceMessages
{
    public static class Success
    {
        public static string Listed(string lang) => lang switch { "ar" => "سجل الحضور", _ => "Attendance" };
        public static string SessionLoaded(string lang) => lang switch { "ar" => "جلسة الحضور", _ => "Attendance session" };
        public static string SessionSaved(string lang) => lang switch { "ar" => "تم حفظ الحضور", _ => "Attendance saved" };
    }

    public static class Errors
    {
        public static string DuplicateSwimmer(string lang) => lang switch { "ar" => "سبّاح مكرر في الطلب", _ => "Duplicate swimmer in request" };
        public static string InvalidStatus(string lang) => lang switch { "ar" => "حالة حضور غير صالحة", _ => "Invalid attendance status" };
    }
}
