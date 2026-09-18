using Kheprx.BaseBackend.Identity.Domain.Exceptions;

namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class AppUser
{
    public Guid Id { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? PasswordHash { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public Guid? GenderId { get; private set; }
    public DateOnly? Dob { get; private set; }
    public string? Phone { get; private set; }
    public Guid RoleId { get; private set; }
    public bool IsFirstLogin { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private AppUser() { } // EF Core

    public AppUser(
        string username, string nameEn, Guid roleId,
        string? nameAr = null, string? email = null, string? passwordHash = null,
        Guid? genderId = null, DateOnly? dob = null, string? phone = null, bool isFirstLogin = true)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new InvalidUserException("Username is required.");
        if (string.IsNullOrWhiteSpace(nameEn)) throw new InvalidUserException("English name is required.");

        Id = Guid.NewGuid();
        Username = username.Trim();
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
        RoleId = roleId;
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        PasswordHash = string.IsNullOrWhiteSpace(passwordHash) ? null : passwordHash;
        GenderId = genderId;
        Dob = dob;
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        IsFirstLogin = isFirstLogin;
        CreatedAt = DateTime.UtcNow;
    }

    public int? Age
    {
        get
        {
            if (Dob is null) return null;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = today.Year - Dob.Value.Year;
            if (Dob.Value > today.AddYears(-age)) age--;
            return age;
        }
    }

    public void SetPassword(string hash)
    {
        PasswordHash = hash;
        IsFirstLogin = false;
    }

    public void UpdateProfile(
        string nameEn, string? nameAr, string? email, Guid? genderId, DateOnly? dob, string? phone)
    {
        if (string.IsNullOrWhiteSpace(nameEn)) throw new InvalidUserException("English name is required.");
        NameEn = nameEn.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        GenderId = genderId;
        Dob = dob;
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
    }
}
