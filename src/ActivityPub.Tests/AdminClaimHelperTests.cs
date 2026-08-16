using System.Security.Claims;
using ActivityPub.WebUI.Services;
using Xunit;

namespace ActivityPub.Tests;

public class AdminClaimHelperTests
{
    [Fact]
    public void IsAdmin_ReturnsFalse_WhenUserIsNull()
    {
        Assert.False(AdminClaimHelper.IsAdmin(null));
    }

    [Fact]
    public void IsAdmin_ReturnsFalse_WhenNoClaims()
    {
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);

        Assert.False(AdminClaimHelper.IsAdmin(principal));
    }

    [Fact]
    public void IsAdmin_ReturnsTrue_WhenAdminClaimPresent()
    {
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(AdminClaimHelper.ClaimType, "true"));
        var principal = new ClaimsPrincipal(identity);

        Assert.True(AdminClaimHelper.IsAdmin(principal));
    }

    [Fact]
    public void IsAdmin_ReturnsFalse_WhenAdminClaimValueIsNotTrue()
    {
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(AdminClaimHelper.ClaimType, "false"));
        var principal = new ClaimsPrincipal(identity);

        Assert.False(AdminClaimHelper.IsAdmin(principal));
    }

    [Fact]
    public void IsAdmin_ReturnsFalse_WhenOtherClaimsPresent()
    {
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(ClaimTypes.Name, "testuser"));
        identity.AddClaim(new Claim(ClaimTypes.Email, "test@example.com"));
        var principal = new ClaimsPrincipal(identity);

        Assert.False(AdminClaimHelper.IsAdmin(principal));
    }

    [Fact]
    public void AddAdminClaim_AddsClaim_WhenIsAdminTrue()
    {
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);

        var result = AdminClaimHelper.AddAdminClaim(principal, true);

        Assert.True(AdminClaimHelper.IsAdmin(result));
        Assert.Contains(result.Claims, c => c.Type == AdminClaimHelper.ClaimType && c.Value == "true");
    }

    [Fact]
    public void AddAdminClaim_DoesNotDuplicate_WhenClaimAlreadyPresent()
    {
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(AdminClaimHelper.ClaimType, "true"));
        var principal = new ClaimsPrincipal(identity);

        var result = AdminClaimHelper.AddAdminClaim(principal, true);

        Assert.Equal(1, result.Claims.Count(c => c.Type == AdminClaimHelper.ClaimType));
    }

    [Fact]
    public void AddAdminClaim_RemovesClaim_WhenIsAdminFalse()
    {
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(AdminClaimHelper.ClaimType, "true"));
        var principal = new ClaimsPrincipal(identity);

        var result = AdminClaimHelper.AddAdminClaim(principal, false);

        Assert.False(AdminClaimHelper.IsAdmin(result));
        Assert.Empty(result.Claims.Where(c => c.Type == AdminClaimHelper.ClaimType));
    }

    [Fact]
    public void AddAdminClaim_LeavesOtherClaimsIntact()
    {
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(ClaimTypes.Name, "testuser"));
        var principal = new ClaimsPrincipal(identity);

        var result = AdminClaimHelper.AddAdminClaim(principal, true);

        Assert.Contains(result.Claims, c => c.Type == ClaimTypes.Name && c.Value == "testuser");
        Assert.True(AdminClaimHelper.IsAdmin(result));
    }

    [Fact]
    public void AddAdminClaim_RemovesAllAdminClaims_WhenIsAdminFalse()
    {
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(AdminClaimHelper.ClaimType, "true"));
        identity.AddClaim(new Claim(AdminClaimHelper.ClaimType, "true"));
        var principal = new ClaimsPrincipal(identity);

        var result = AdminClaimHelper.AddAdminClaim(principal, false);

        Assert.Empty(result.Claims.Where(c => c.Type == AdminClaimHelper.ClaimType));
    }

    [Fact]
    public void ClaimType_IsAppIsAdmin()
    {
        Assert.Equal("app.isadmin", AdminClaimHelper.ClaimType);
    }
}
