using System.Globalization;

namespace Kheprx.BaseBackend.SharedKernel.Resources;

/// <summary>App-wide response language, resolved per request from the current UI culture — which the
/// request-localization middleware sets from the client's Accept-Language header. Returns the two-letter
/// code ("en" or "ar"); message lookups treat anything other than "ar" as English. Unsupported or missing
/// headers fall back to the middleware's default culture (en).</summary>
public static class AppLanguage
{
    public static string Current => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
}
