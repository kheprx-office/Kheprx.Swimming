namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

// Builds the PostgreSQL ILike patterns used by the Identity read queries (UserRepository,
// WorkerReadRepository) so the wildcard escaping (\, %, _) lives in exactly one place.
// Backslash is escaped FIRST, so the backslashes added for % and _ are not doubled.
// Name → "%term%" (contains);  Nid → "term%" (starts-with).
internal static class SearchPatterns
{
    public static (string Name, string Nid) ForNameAndNid(string search)
    {
        var term = search.Trim()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        return ($"%{term}%", $"{term}%");
    }
}
