using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Exceptions;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public sealed class AppUserTests
{
    private static AppUser New(bool firstLogin = true) =>
        new("captain.dave", "Dave Coach", Guid.NewGuid(),
            nameAr: "ديف", email: "Dave@Oasis.com", passwordHash: "hash", isFirstLogin: firstLogin);

    [Fact]
    public void Ctor_normalizes_email_and_trims_names()
    {
        var u = New();
        Assert.Equal("dave@oasis.com", u.Email);
        Assert.Equal("Dave Coach", u.NameEn);
        Assert.True(u.IsFirstLogin);
    }

    [Fact]
    public void SetPassword_clears_first_login()
    {
        var u = New(firstLogin: true);
        u.SetPassword("newhash");
        Assert.Equal("newhash", u.PasswordHash);
        Assert.False(u.IsFirstLogin);
    }

    [Fact]
    public void Ctor_requires_username_and_name_en()
    {
        Assert.Throws<InvalidUserException>(() => new AppUser("", "Dave", Guid.NewGuid()));
        Assert.Throws<InvalidUserException>(() => new AppUser("dave", " ", Guid.NewGuid()));
    }

    [Fact]
    public void Age_is_null_when_no_dob_and_computed_when_present()
    {
        Assert.Null(New().Age);
        var born = new AppUser("u", "n", Guid.NewGuid(), dob: new DateOnly(2000, 1, 1));
        Assert.True(born.Age >= 25);
    }
}
