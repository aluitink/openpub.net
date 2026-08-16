using ActivityPub.Core.Interfaces;
using ActivityPub.Core.Models;
using ActivityPub.Core.Services;
using ActivityPub.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ActivityPub.WebUI.Controllers;

public class AuthController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IActivityPubRepository _activityPubRepository;
    private readonly ILogger<AuthController> _logger;
    private readonly IKeyGenerationService _keyGenerationService;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IActivityPubRepository activityPubRepository,
        ILogger<AuthController> logger,
        IKeyGenerationService keyGenerationService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _activityPubRepository = activityPubRepository;
        _logger = logger;
        _keyGenerationService = keyGenerationService;
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(model.Username) || model.Username.Length < 3 || model.Username.Length > 30)
        {
            ModelState.AddModelError("Username", "Username must be 3-30 characters.");
            return View(model);
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(model.Username, @"^[a-zA-Z0-9_]+$"))
        {
            ModelState.AddModelError("Username", "Username can only contain letters, numbers, and underscores.");
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(model.DisplayName) || model.DisplayName.Length > 50)
        {
            ModelState.AddModelError("DisplayName", "Display name is required and must be 50 characters or less.");
            return View(model);
        }

        var existingUser = await _userManager.FindByNameAsync(model.Username);
        if (existingUser != null)
        {
            ModelState.AddModelError("Username", "Username is already taken.");
            return View(model);
        }

        var existingEmail = await _userManager.FindByEmailAsync(model.Email);
        if (existingEmail != null)
        {
            ModelState.AddModelError("Email", "Email is already registered.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Username,
            Email = model.Email,
            DisplayName = model.DisplayName,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            return View(model);
        }

        try
        {
            var (privateKeyPem, publicKeyPem) = _keyGenerationService.GenerateRSAKeyPair();
            var now = DateTime.UtcNow;
            var actor = new Actor
            {
                Id = $"https://localhost/users/{model.Username}",
                Type = "Person",
                PreferredUsername = model.Username,
                Name = model.DisplayName,
                Summary = "",
                Inbox = $"https://localhost/inbox/{model.Username}",
                Outbox = $"https://localhost/outbox/{model.Username}",
                Followers = $"https://localhost/followers/{model.Username}",
                Following = $"https://localhost/following/{model.Username}",
                Liked = $"https://localhost/liked/{model.Username}",
                PublicKey = new PublicKey
                {
                    Id = $"https://localhost/users/{model.Username}#main-key",
                    Owner = $"https://localhost/users/{model.Username}",
                    PublicKeyPem = publicKeyPem
                },
                Url = $"https://localhost/@{model.Username}",
                ManuallyApprovesFollowers = false,
                Published = now,
                Updated = now
            };

            if (actor.AdditionalProperties == null)
                actor.AdditionalProperties = new Dictionary<string, JsonElement>();
            actor.AdditionalProperties["privateKeyPem"] = JsonSerializer.SerializeToElement(privateKeyPem);

            await _activityPubRepository.SaveUserActorAsync(actor);
            user.ActorId = actor.Id;
            await _userManager.UpdateAsync(user);
            _logger.LogInformation("Created actor {ActorId} for user {Username}", actor.Id, model.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create actor for user {Username}", model.Username);
            await _userManager.DeleteAsync(user);
            ModelState.AddModelError("", "Failed to create federation account. Please try again.");
            return View(model);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        _logger.LogInformation("User {Username} registered and signed in.", model.Username);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginModel { ReturnUrl = returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginModel model, string? returnUrl = null)
    {
        if (string.IsNullOrEmpty(returnUrl))
            returnUrl = "/";

        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Username, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Username} logged in.", model.Username);
            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User {Username} account locked out.", model.Username);
            ModelState.AddModelError("", "Account is locked out.");
            return View(model);
        }

        ModelState.AddModelError("", "Invalid login attempt.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
        return RedirectToAction("Index", "Home");
    }
}
