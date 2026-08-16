using System.Security.Claims;
using ActivityPub.WebUI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.WebUI.Components;

public class AdminSection : ViewComponent
{
    public Task<IViewComponentResult> InvokeAsync()
    {
        var principal = User as ClaimsPrincipal;
        if (!AdminClaimHelper.IsAdmin(principal))
            return Task.FromResult<IViewComponentResult>(Content(""));

        var html = "<li class=\"nav-group nav-group-admin\">" +
            "<button type=\"button\" class=\"nav-group-toggle\" aria-expanded=\"false\" aria-haspopup=\"true\">" +
            "Admin <span class=\"caret\" aria-hidden=\"true\">&#9662;</span></button>" +
            "<ul class=\"nav-dropdown\" role=\"menu\">" +
            "<li><a href=\"/admin/dashboard\" class=\"nav-dropdown-link\" role=\"menuitem\">Dashboard</a></li>" +
            "<li><a href=\"/admin/users\" class=\"nav-dropdown-link\" role=\"menuitem\">Users</a></li>" +
            "<li><a href=\"/admin/moderation\" class=\"nav-dropdown-link\" role=\"menuitem\">Moderation</a></li>" +
            "<li><a href=\"/admin/reports\" class=\"nav-dropdown-link\" role=\"menuitem\">Reports</a></li>" +
            "<li><a href=\"/admin/audit-log\" class=\"nav-dropdown-link\" role=\"menuitem\">Audit Log</a></li>" +
            "<li><a href=\"/mrf\" class=\"nav-dropdown-link\" role=\"menuitem\">MRF</a></li>" +
            "<li><a href=\"/rate-limits\" class=\"nav-dropdown-link\" role=\"menuitem\">Rate Limits</a></li>" +
            "<li><a href=\"/federation-health\" class=\"nav-dropdown-link\" role=\"menuitem\">Federation Health</a></li>" +
            "</ul></li>";

        return Task.FromResult<IViewComponentResult>(Content(html));
    }
}
