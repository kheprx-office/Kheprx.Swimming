namespace Kheprx.BaseBackend.SharedKernel.Resources;

/// <summary>App-wide response language. Arabic-only for now; later this resolves per request (Accept-Language).</summary>
public static class AppLanguage
{
    public static string Current => "ar";
}
