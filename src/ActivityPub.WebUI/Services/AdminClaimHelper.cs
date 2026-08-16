using System.Security.Claims;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Identity;

namespace ActivityPub.WebUI.Services;

public static class AdminClaimHelper
{
    public const string ClaimType = "app.isadmin";

    public static bool IsAdmin(ClaimsPrincipal? user)
        => user?.Claims?.Any(c => c.Type == ClaimType && c.Value == "true") ?? false;

    public static ClaimsPrincipal AddAdminClaim(ClaimsPrincipal principal, bool isAdmin)
    {
        var identity = (ClaimsIdentity)principal.Identity!;
        var existing = identity.FindAll(ClaimType).ToList();
        foreach (var claim in existing)
            identity.RemoveClaim(claim);

        if (isAdmin)
            identity.AddClaim(new Claim(ClaimType, "true"));

        return principal;
    }

    public static Task SignInWithClaimsAsync<TUser>(SignInManager<TUser> signInManager, TUser user, bool isPersistent = false)
        where TUser : class
    {
        var claims = new List<Claim>();
        if (user is ApplicationUser appUser && appUser.IsAdmin)
            claims.Add(new Claim(ClaimType, "true"));

        return signInManager.SignInWithClaimsAsync(user, isPersistent, claims);
    }
}
