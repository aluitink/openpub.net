using System.Net.Mime;
using ActivityPub.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivityPub.WebUI.Controllers;

[Authorize]
public class UploadController : Controller
{
    private readonly IActivityPubRepository _repository;
    private readonly ILogger<UploadController> _logger;
    private readonly string _uploadPath;

    public UploadController(IActivityPubRepository repository, ILogger<UploadController> logger, IWebHostEnvironment env)
    {
        _repository = repository;
        _logger = logger;
        _uploadPath = Path.Combine(env.ContentRootPath, "wwwroot", "uploads");
        Directory.CreateDirectory(_uploadPath);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided" });
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new { error = "File too large (max 10MB)" });
        }

        var allowed = new[] { MediaTypeNames.Image.Jpeg, MediaTypeNames.Image.Png, MediaTypeNames.Image.Gif, "image/webp" };
        if (!allowed.Contains(file.ContentType))
        {
            return BadRequest(new { error = "Unsupported file type" });
        }

        var extension = Path.GetExtension(file.FileName);
        var filename = $"{Guid.NewGuid():N}{extension}";
        var filepath = Path.Combine(_uploadPath, filename);

        using var stream = new FileStream(filepath, FileMode.Create);
        await file.CopyToAsync(stream);

        var url = $"/uploads/{filename}";

        var image = new
        {
            type = "Image",
            url,
            mediaType = file.ContentType,
            name = file.FileName
        };

        return Ok(new { success = true, url, image });
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(string filename)
    {
        var filepath = Path.Combine(_uploadPath, filename);
        if (System.IO.File.Exists(filepath))
        {
            System.IO.File.Delete(filepath);
        }
        return Ok(new { success = true });
    }
}
