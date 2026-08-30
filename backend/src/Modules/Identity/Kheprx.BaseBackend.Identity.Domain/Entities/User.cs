using Kheprx.BaseBackend.Identity.Domain.Exceptions;

namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public string? Code { get; private set; }
    public string? Email { get; private set; }
    public string? PasswordHash { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Gender { get; private set; }
    public int? Age { get; private set; }
    public string Nid { get; private set; } = string.Empty;
    public Guid RoleId { get; private set; }
    public bool IsActive { get; private set; }
    public bool MustChangePassword { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private User() { } // EF Core

    public User(
        string fullName, Guid roleId, string nid, string? email = null, string? passwordHash = null,
        string? phone = null, string? gender = null, int? age = null, bool mustChangePassword = false)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new InvalidUserException("Full name is required.");
        if (string.IsNullOrWhiteSpace(nid))
            throw new InvalidUserException("National ID is required.");

        Id = Guid.NewGuid();
        FullName = fullName.Trim();
        RoleId = roleId;
        Nid = nid.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        PasswordHash = string.IsNullOrWhiteSpace(passwordHash) ? null : passwordHash;
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Gender = string.IsNullOrWhiteSpace(gender) ? null : gender;
        Age = age;
        MustChangePassword = mustChangePassword;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void AssignCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidUserException("Code is required.");
        Code = code.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(
        string fullName, string? email, string nid,
        string? phone = null, string? gender = null, int? age = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new InvalidUserException("Full name is required.");
        if (string.IsNullOrWhiteSpace(nid))
            throw new InvalidUserException("National ID is required.");

        FullName = fullName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        Nid = nid.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Gender = string.IsNullOrWhiteSpace(gender) ? null : gender;
        Age = age;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPassword(string hash)
    {
        PasswordHash = hash;
        MustChangePassword = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
