namespace Kheprx.BaseBackend.Attendance.Application.Resources;

/// <summary>Localized messages for attendance queries.</summary>
public static class AttendanceMessages
{
    public static class Success
    {
        public static string Listed(string lang) => lang switch { "ar" => "سجل الحضور", _ => "Attendance" };
    }
}
