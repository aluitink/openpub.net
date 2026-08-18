using ActivityPub.Core.Implementations;
using ActivityPub.Core.Repositories;
using Xunit;

namespace ActivityPub.Tests.Repositories;

/// <summary>
/// Unit tests for <see cref="InMemoryApplicationRepository"/> — the OAuth
/// client / authorization-code / access-token store, which previously had no
/// direct unit test. Covers client CRUD + secret verification, owner-based
/// listing, authorization-code redemption (single-use + expiry), and access
/// token retrieval (with expiry).
/// </summary>
public class InMemoryApplicationRepositoryTests
{
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
        var repo = new InMemoryApplicationRepository();
        var client = Client();

        Assert.True(await repo.SaveApplicationAsync(client));

        var fetched = await repo.GetApplicationAsync("client-1");
        Assert.NotNull(fetched);
        Assert.Equal("Test App", fetched!.Name);
        Assert.Equal("secret-1", fetched.ClientSecret);
    }

    [Fact]
    public async Task SaveClient_NullOrEmptyClientId_ReturnsFalse()
    {
        var repo = new InMemoryApplicationRepository();

        Assert.False(await repo.SaveApplicationAsync(null!));
        Assert.False(await repo.SaveApplicationAsync(new OAuthClientEntity { ClientId = "" }));

        Assert.Empty(await repo.GetAllAsync());
    }

    [Fact]
    public async Task GetApplication_UnknownOrEmpty_ReturnsNull()
    {
        var repo = new InMemoryApplicationRepository();
        await repo.SaveApplicationAsync(Client());

        Assert.Null(await repo.GetApplicationAsync("missing"));
        Assert.Null(await repo.GetApplicationAsync(""));
    }

    [Fact]
    public async Task VerifyClient_MatchesSecret_ReturnsTrue()
    {
        var repo = new InMemoryApplicationRepository();
        await repo.SaveApplicationAsync(Client(secret: "right-secret"));

        Assert.True(await repo.VerifyClientAsync("client-1", "right-secret"));
        Assert.False(await repo.VerifyClientAsync("client-1", "wrong-secret"));
    }

    [Fact]
    public async Task VerifyClient_UnknownOrEmpty_ReturnsFalse()
    {
        var repo = new InMemoryApplicationRepository();
        await repo.SaveApplicationAsync(Client());

        Assert.False(await repo.VerifyClientAsync("missing", "secret-1"));
        Assert.False(await repo.VerifyClientAsync("", "secret-1"));
        Assert.False(await repo.VerifyClientAsync("client-1", ""));
    }

    [Fact]
    public async Task GetAll_ReturnsEveryClient()
    {
        var repo = new InMemoryApplicationRepository();
        await repo.SaveApplicationAsync(Client("a", "sa"));
        await repo.SaveApplicationAsync(Client("b", "sb"));

        Assert.Equal(2, (await repo.GetAllAsync()).Count);
    }

    [Fact]
    public async Task GetByOwner_FiltersByOwner()
    {
        var repo = new InMemoryApplicationRepository();
        await repo.SaveApplicationAsync(Client("a", "sa", owner: "https://me/users/alice"));
        await repo.SaveApplicationAsync(Client("b", "sb", owner: "https://me/users/bob"));
        await repo.SaveApplicationAsync(Client("c", "sc", owner: null));

        var aliceClients = await repo.GetByOwnerAsync("https://me/users/alice");
        var bobClients = await repo.GetByOwnerAsync("https://me/users/bob");

        var aliceIds = aliceClients.Select(c => c.ClientId).ToList();
        var bobIds = bobClients.Select(c => c.ClientId).ToList();

        Assert.Equal(new[] { "a" }, aliceIds);
        Assert.Equal(new[] { "b" }, bobIds);
    }

    [Fact]
    public async Task GetByOwner_UnknownOrEmpty_ReturnsEmpty()
    {
        var repo = new InMemoryApplicationRepository();
        await repo.SaveApplicationAsync(Client(owner: "https://me/users/alice"));

        Assert.Empty(await repo.GetByOwnerAsync("https://me/users/unknown"));
        Assert.Empty(await repo.GetByOwnerAsync(""));
    }

    // --- Authorization codes ---------------------------------------------

    [Fact]
    public async Task RedeemAuthorizationCode_Valid_ReturnsCode_MarksUsed()
    {
        var repo = new InMemoryApplicationRepository();
        await repo.SaveAuthorizationCodeAsync(Code());

        var redeemed = await repo.RedeemAuthorizationCodeAsync("auth-code-1");

        Assert.NotNull(redeemed);
        Assert.Equal("alice", redeemed!.Username);
        Assert.True(redeemed.IsUsed);
    }

    [Fact]
    public async Task RedeemAuthorizationCode_AlreadyUsed_ReturnsNull()
    {
        var repo = new InMemoryApplicationRepository();
        await repo.SaveAuthorizationCodeAsync(Code(used: true));

        Assert.Null(await repo.RedeemAuthorizationCodeAsync("auth-code-1"));
    }

    [Fact]
    public async Task RedeemAuthorizationCode_Expired_ReturnsNull()
    {
        var repo = new InMemoryApplicationRepository();
        await repo.SaveAuthorizationCodeAsync(Code(expiresAt: DateTime.UtcNow.AddMinutes(-1)));

        Assert.Null(await repo.RedeemAuthorizationCodeAsync("auth-code-1"));
    }

    [Fact]
    public async Task RedeemAuthorizationCode_SingleUse_SecondRedeemReturnsNull()
    {
        var repo = new InMemoryApplicationRepository();
        await repo.SaveAuthorizationCodeAsync(Code());

        Assert.NotNull(await repo.RedeemAuthorizationCodeAsync("auth-code-1"));
        Assert.Null(await repo.RedeemAuthorizationCodeAsync("auth-code-1"));
    }

    [Fact]
    public async Task SaveAuthorizationCode_NullOrEmpty_ReturnsFalse()
    {
        var repo = new InMemoryApplicationRepository();

        Assert.False(await repo.SaveAuthorizationCodeAsync(null!));
        Assert.False(await repo.SaveAuthorizationCodeAsync(new OAuthCodeEntity { Code = "" }));

        Assert.Null(await repo.RedeemAuthorizationCodeAsync("auth-code-1"));
    }

    // --- Access tokens ----------------------------------------------------

    [Fact]
    public async Task SaveAndGet_AccessTokenRoundTrips()
    {
        var repo = new InMemoryApplicationRepository();
        await repo.SaveAccessTokenAsync(Token());

        var fetched = await repo.GetAccessTokenAsync("access-token-1");
        Assert.NotNull(fetched);
        Assert.Equal("alice", fetched!.Username);
    }

    [Fact]
    public async Task GetAccessToken_Expired_ReturnsNull()
    {
        var repo = new InMemoryApplicationRepository();
        await repo.SaveAccessTokenAsync(Token(expiresAt: DateTime.UtcNow.AddMinutes(-1)));

        Assert.Null(await repo.GetAccessTokenAsync("access-token-1"));
    }

    [Fact]
    public async Task GetAccessToken_UnknownOrEmpty_ReturnsNull()
    {
        var repo = new InMemoryApplicationRepository();
        await repo.SaveAccessTokenAsync(Token());

        Assert.Null(await repo.GetAccessTokenAsync("missing"));
        Assert.Null(await repo.GetAccessTokenAsync(""));
    }

    [Fact]
    public async Task SaveAccessToken_NullOrEmpty_ReturnsFalse()
    {
        var repo = new InMemoryApplicationRepository();

        Assert.False(await repo.SaveAccessTokenAsync(null!));
        Assert.False(await repo.SaveAccessTokenAsync(new OAuthTokenEntity { Token = "" }));

        Assert.Null(await repo.GetAccessTokenAsync("access-token-1"));
    }
}
