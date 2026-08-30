using Kheprx.BaseBackend.Identity.Infrastructure.Security;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_verify_roundtrips()
        => Assert.True(_hasher.Verify("s3cret!", _hasher.Hash("s3cret!")));

    [Fact]
    public void Verify_rejects_wrong_password()
        => Assert.False(_hasher.Verify("wrong", _hasher.Hash("s3cret!")));

    [Fact]
    public void Hash_uses_random_salt_so_two_hashes_differ()
        => Assert.NotEqual(_hasher.Hash("same"), _hasher.Hash("same"));

    [Fact]
    public void Verify_returns_false_for_malformed_hash()
        => Assert.False(_hasher.Verify("x", "not-a-valid-hash"));
}
