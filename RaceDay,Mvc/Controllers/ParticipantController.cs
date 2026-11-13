
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

[SessionAuthorize(RequiredRole = "Participant")]
public class ParticipantController : Controller
{
    private readonly ApiService _api;

    public ParticipantController(ApiService api)
    {
        _api = api;
    }

    // Participant dashboard.
    [HttpGet]
    public IActionResult Dashboard()
    {
        return RedirectToAction(nameof(MyEvents));
    }

    // Display participant events.
    [HttpGet]
    public async Task<IActionResult> MyEvents()
    {
        var enrolments = await _api.GetAsync<object>(
            "api/enrolments/me");

        return View(enrolments);
    }

    // Display participant results.
    [HttpGet]
    public async Task<IActionResult> MyResults()
    {
        var results = await _api.GetAsync<object>(
            "api/results/me");

        return View(results);
    }

    // Display participant profile.
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var profile = await _api.GetAsync<System.Text.Json.JsonElement>(
            "api/users/me");

        return View(profile);
    }

    // Retrieve the participant's profile picture.
    [HttpGet]
    public async Task<IActionResult> ProfilePictureImage()
    {
        var image = await _api.GetBytesAsync(
            "api/users/me/profile-picture");

        if (image == null || image.Length == 0)
            return NotFound();

        return File(image, "image/png");
    }

    // Upload a new participant profile picture.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProfilePicture(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["ProfileMessage"] =
                "Please select an image.";

            return RedirectToAction(nameof(Profile));
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            TempData["ProfileMessage"] =
                "The image must not exceed 5 MB.";

            return RedirectToAction(nameof(Profile));
        }

        var allowedTypes = new[]
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            TempData["ProfileMessage"] =
                "Please upload a JPG, PNG or WEBP image.";

            return RedirectToAction(nameof(Profile));
        }

        var uploaded = await _api.UploadAsync(
            "api/users/me/profile-picture",
            file);

        TempData["ProfileMessage"] = uploaded
            ? "Profile picture uploaded successfully."
            : "Profile picture upload failed. Please try again.";

        return RedirectToAction(nameof(Profile));
    }
}
