using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Exceptions;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class UserTests
{
    [Fact]
    public void Ctor_generates_id_and_defaults_active_and_no_force_change()
    {
        var user = new User("Alice", Guid.NewGuid(), "29801014501237", email: "a@b.com", passwordHash: "hash");
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.True(user.IsActive);
        Assert.False(user.MustChangePassword);
        Assert.Null(user.LastLoginAt);
        Assert.Equal("a@b.com", user.Email);
    }

    [Fact]
    public void Ctor_allows_null_email_and_password_for_login_less_person()
    {
        var user = new User("Bob", Guid.NewGuid(), "29801014501238");
        Assert.Null(user.Email);
        Assert.Null(user.PasswordHash);
    }

    [Fact]
    public void Ctor_lowercases_and_trims_email()
    {
        var user = new User("Alice", Guid.NewGuid(), "29801014501239", email: "  A@B.Com ");
        Assert.Equal("a@b.com", user.Email);
    }

    [Fact]
    public void Ctor_requires_full_name()
        => Assert.Throws<InvalidUserException>(() => new User(" ", Guid.NewGuid(), "29801014501240"));

    [Fact]
    public void RecordLogin_sets_last_login_and_touches()
    {
        var user = new User("Alice", Guid.NewGuid(), "29801014501241", email: "a@b.com", passwordHash: "hash");
        user.RecordLogin();
        Assert.NotNull(user.LastLoginAt);
        Assert.NotNull(user.UpdatedAt);
    }

    [Fact]
    public void SetPassword_sets_hash_and_clears_must_change_flag()
    {
        var user = new User("Alice", Guid.NewGuid(), "29801014501242", email: "a@b.com", passwordHash: "old", mustChangePassword: true);
        user.SetPassword("new");
        Assert.Equal("new", user.PasswordHash);
        Assert.False(user.MustChangePassword);
        Assert.NotNull(user.UpdatedAt);
    }

    [Fact]
    public void UpdateProfile_updates_name_and_normalizes_email_and_touches()
    {
        var user = new User("Alice", Guid.NewGuid(), "29801014501243", email: "a@b.com", passwordHash: "h");
        user.UpdateProfile("  Bob  ", "  B@C.Com ", user.Nid);
        Assert.Equal("Bob", user.FullName);
        Assert.Equal("b@c.com", user.Email);
        Assert.NotNull(user.UpdatedAt);
    }

    [Fact]
    public void UpdateProfile_requires_full_name()
    {
        var user = new User("Alice", Guid.NewGuid(), "29801014501244");
        Assert.Throws<InvalidUserException>(() => user.UpdateProfile(" ", "a@b.com", user.Nid));
    }

    [Fact]
    public void UpdateProfile_preserves_phone_when_omitted()
    {
        var user = new User("Alice", Guid.NewGuid(), "29801014501245", phone: "0100000001");
        user.UpdateProfile("Alice", "a@b.com", user.Nid, phone: "0100000001");
        Assert.Equal("0100000001", user.Phone);
    }

    [Fact]
    public void UpdateProfile_sets_email_null_when_blank()
    {
        var user = new User("Alice", Guid.NewGuid(), "29801014501246", email: "a@b.com");
        user.UpdateProfile("Alice", "   ", user.Nid);
        Assert.Null(user.Email);
    }

    [Fact]
    public void Activate_flips_inactive_back_to_active()
    {
        var user = new User("Alice", Guid.NewGuid(), "29801014501247");
        user.Deactivate();
        Assert.False(user.IsActive);
        user.Activate();
        Assert.True(user.IsActive);
    }

    [Fact]
    public void Activate_on_fresh_user_stamps_updated_at()
    {
        var user = new User("Alice", Guid.NewGuid(), "29801014501248"); // fresh: UpdatedAt is null
        Assert.Null(user.UpdatedAt);
        user.Activate();
        Assert.NotNull(user.UpdatedAt); // only Activate could have set it
    }

    [Fact]
    public void Ctor_sets_nid_gender_age_and_trims_nid()
    {
        var user = new User("Alice", Guid.NewGuid(), " 29801014501234 ", gender: "male", age: 30);
        Assert.Equal("29801014501234", user.Nid);
        Assert.Equal("male", user.Gender);
        Assert.Equal(30, user.Age);
        Assert.Null(user.Code);
    }

    [Fact]
    public void Ctor_throws_when_nid_missing()
    {
        Assert.Throws<InvalidUserException>(() => new User("Alice", Guid.NewGuid(), "  "));
    }

    [Fact]
    public void AssignCode_sets_code()
    {
        var user = new User("Alice", Guid.NewGuid(), "29801014501234");
        user.AssignCode("W-1");
        Assert.Equal("W-1", user.Code);
    }

    [Fact]
    public void UpdateProfile_updates_person_fields_and_clears_optional_ones()
    {
        var user = new User("Alice", Guid.NewGuid(), "29801014501234",
            phone: "0100000001", gender: "male", age: 30);

        user.UpdateProfile("Alice B", "a@b.com", "29801014509999", phone: null, gender: null, age: null);

        Assert.Equal("Alice B", user.FullName);
        Assert.Equal("a@b.com", user.Email);
        Assert.Equal("29801014509999", user.Nid);
        Assert.Null(user.Phone);
        Assert.Null(user.Gender);
        Assert.Null(user.Age);
        Assert.NotNull(user.UpdatedAt);
    }

    [Fact]
    public void UpdateProfile_throws_when_nid_missing()
    {
        var user = new User("Alice", Guid.NewGuid(), "29801014501234");
        Assert.Throws<InvalidUserException>(() => user.UpdateProfile("Alice", "a@b.com", " "));
    }
}
