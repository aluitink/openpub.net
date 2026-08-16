using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ActivityPub.WebUI.Models;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string? BannerUrl { get; set; }
    public string? ActorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsAdmin { get; set; }
    public bool IsBlocked { get; set; }
}
