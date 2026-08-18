using ActivityPub.Core.Implementations;
using ActivityPub.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ActivityPub.Tests.Repositories;

/// <summary>
/// Unit tests for <see cref="EFCoreApplicationRepository"/> — the EF Core
/// <c>IApplicationRepository</c> implementation (the production OAuth client /
/// authorization-code / access-token store backed by
/// <see cref="ActivityPubDbContext"/>), which previously had no direct unit
/// test. Uses a real <c>ActivityPubDbContext</c> over the EF Core in-memory
/// provider so the LINQ queries execute against a genuine (offline) database.
/// Mirrors the contract pinned for <c>InMemoryApplicationRepository</c>.
/// </summary>
public class EFCoreApplicationRepositoryTests
{
    private readonly EFCoreApplicationRepository _repo;

    public EFCoreApplicationRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ActivityPubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _repo = new EFCoreApplicationRepository(new ActivityPubDbContext(options));
    }

    private static OAuthClientEntity Client(
        string clientId = "client-1",
        string secret = "secret-1",
        string? owner = null) =>
        new() { ClientId = clientId, ClientSecret = secret, Name = "Test App", OwnerActorId = owner };

    private static OAuthCodeEntity Code(
        string code = "auth-code-1",
        DateTime? expiresAt = null,
        bool used = false) =>
        new() { Code = code, Username = "alice", ClientId = "client-1", Scopes = "read write",
                ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMinutes(10), IsUsed = used };

    private static OAuthTokenEntity Token(
        string token = "access-token-1",
        DateTime? expiresAt = null) =>
        new() { Token = token, Username = "alice", ClientId = "client-1", Scopes = "read write",
                ExpiresAt = expiresAt ?? DateTime.UtcNow.AddHours(1) };

    // --- Client applications ---------------------------------------------

    [Fact]
    public async Task SaveAndGet_RoundTripsClient()
    {
        Assert.True(await _repo.SaveApplicationAsync(Client()));

        var fetched = await _repo.GetApplicationAsync("client-1");
        Assert.NotNull(fetched);
        Assert.Equal("Test App", fetched!.Name);
        Assert.Equal("secret-1", fetched.ClientSecret);
    }

    [Fact]
    public async Task SaveClient_NullOrEmptyClientId_ReturnsFalse()
    {
        Assert.False(await _repo.SaveApplicationAsync(null!));
        Assert.False(await _repo.SaveApplicationAsync(new OAuthClientEntity { ClientId = "" }));

        Assert.Empty(await _repo.GetAllAsync());
    }

    [Fact]
    public async Task GetApplication_UnknownOrEmpty_ReturnsNull()
    {
        await _repo.SaveApplicationAsync(Client());

        Assert.Null(await _repo.GetApplicationAsync("missing"));
        Assert.Null(await _repo.GetApplicationAsync(""));
    }

    [Fact]
    public async Task VerifyClient_MatchesSecret_ReturnsTrue()
    {
        await _repo.SaveApplicationAsync(Client(secret: "right-secret"));

        Assert.True(await _repo.VerifyClientAsync("client-1", "right-secret"));
        Assert.False(await _repo.VerifyClientAsync("client-1", "wrong-secret"));
    }

    [Fact]
    public async Task VerifyClient_UnknownOrEmpty_ReturnsFalse()
    {
        await _repo.SaveApplicationAsync(Client());

        Assert.False(await _repo.VerifyClientAsync("missing", "secret-1"));
        Assert.False(await _repo.VerifyClientAsync("", "secret-1"));
        Assert.False(await _repo.VerifyClientAsync("client-1", ""));
    }

    [Fact]
    public async Task GetAll_ReturnsEveryClient()
    {
        await _repo.SaveApplicationAsync(Client("a", "sa"));
        await _repo.SaveApplicationAsync(Client("b", "sb"));

        Assert.Equal(2, (await _repo.GetAllAsync()).Count);
    }

    [Fact]
    public async Task GetByOwner_FiltersByOwner()
    {
        await _repo.SaveApplicationAsync(Client("a", "sa", owner: "https://me/users/alice"));
        await _repo.SaveApplicationAsync(Client("b", "sb", owner: "https://me/users/bob"));
        await _repo.SaveApplicationAsync(Client("c", "sc", owner: null));

        var aliceIds = (await _repo.GetByOwnerAsync("https://me/users/alice")).Select(c => c.ClientId).ToList();
        var bobIds = (await _repo.GetByOwnerAsync("https://me/users/bob")).Select(c => c.ClientId).ToList();

        Assert.Equal(new[] { "a" }, aliceIds);
        Assert.Equal(new[] { "b" }, bobIds);
    }

    [Fact]
    public async Task GetByOwner_UnknownOrEmpty_ReturnsEmpty()
    {
        await _repo.SaveApplicationAsync(Client(owner: "https://me/users/alice"));

        Assert.Empty(await _repo.GetByOwnerAsync("https://me/users/unknown"));
        Assert.Empty(await _repo.GetByOwnerAsync(""));
    }

    // --- Authorization codes ---------------------------------------------

    [Fact]
    public async Task RedeemAuthorizationCode_Valid_ReturnsCode_MarksUsed()
    {
        await _repo.SaveAuthorizationCodeAsync(Code());

        var redeemed = await _repo.RedeemAuthorizationCodeAsync("auth-code-1");

        Assert.NotNull(redeemed);
        Assert.Equal("alice", redeemed!.Username);
        Assert.True(redeemed.IsUsed);
    }

    [Fact]
    public async Task RedeemAuthorizationCode_AlreadyUsed_ReturnsNull()
    {
        await _repo.SaveAuthorizationCodeAsync(Code(used: true));

        Assert.Null(await _repo.RedeemAuthorizationCodeAsync("auth-code-1"));
    }

    [Fact]
    public async Task RedeemAuthorizationCode_Expired_ReturnsNull()
    {
        await _repo.SaveAuthorizationCodeAsync(Code(expiresAt: DateTime.UtcNow.AddMinutes(-1)));

        Assert.Null(await _repo.RedeemAuthorizationCodeAsync("auth-code-1"));
    }

    [Fact]
    public async Task RedeemAuthorizationCode_SingleUse_SecondRedeemReturnsNull()
    {
        await _repo.SaveAuthorizationCodeAsync(Code());

        Assert.NotNull(await _repo.RedeemAuthorizationCodeAsync("auth-code-1"));
        Assert.Null(await _repo.RedeemAuthorizationCodeAsync("auth-code-1"));
    }

    [Fact]
    public async Task SaveAuthorizationCode_NullOrEmpty_ReturnsFalse()
    {
        Assert.False(await _repo.SaveAuthorizationCodeAsync(null!));
        Assert.False(await _repo.SaveAuthorizationCodeAsync(new OAuthCodeEntity { Code = "" }));

        Assert.Null(await _repo.RedeemAuthorizationCodeAsync("auth-code-1"));
    }

    // --- Access tokens ----------------------------------------------------

    [Fact]
    public async Task SaveAndGet_AccessTokenRoundTrips()
    {
        await _repo.SaveAccessTokenAsync(Token());

        var fetched = await _repo.GetAccessTokenAsync("access-token-1");
        Assert.NotNull(fetched);
        Assert.Equal("alice", fetched!.Username);
    }

    [Fact]
    public async Task GetAccessToken_Expired_ReturnsNull()
    {
        await _repo.SaveAccessTokenAsync(Token(expiresAt: DateTime.UtcNow.AddMinutes(-1)));

        Assert.Null(await _repo.GetAccessTokenAsync("access-token-1"));
    }

    [Fact]
    public async Task GetAccessToken_UnknownOrEmpty_ReturnsNull()
    {
        await _repo.SaveAccessTokenAsync(Token());

        Assert.Null(await _repo.GetAccessTokenAsync("missing"));
        Assert.Null(await _repo.GetAccessTokenAsync(""));
    }

    [Fact]
    public async Task SaveAccessToken_NullOrEmpty_ReturnsFalse()
    {
        Assert.False(await _repo.SaveAccessTokenAsync(null!));
        Assert.False(await _repo.SaveAccessTokenAsync(new OAuthTokenEntity { Token = "" }));

        Assert.Null(await _repo.GetAccessTokenAsync("access-token-1"));
    }
}
