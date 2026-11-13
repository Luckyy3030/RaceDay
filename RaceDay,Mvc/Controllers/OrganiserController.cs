


using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

[SessionAuthorize(RequiredRole = "Organiser")]
public class OrganiserController : Controller
{
    private readonly ApiService _api;

    public OrganiserController(ApiService api)
    {
        _api = api;
    }

    // Display organiser dashboard and events.
    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var events = await _api.GetAsync<List<EventViewModel>>(
            "api/events");

        return View(events ?? new List<EventViewModel>());
    }

    // Display create event form.
    [HttpGet]
    public IActionResult CreateEvent()
    {
        return View(new EventViewModel());
    }

    // Create a new event.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(EventViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var eventRequest = new
        {
            EventName = model.Name,
            Description = model.Description,
            EventDate = model.EventDate.ToString("yyyy-MM-dd"),
            Location = model.Location,
            EventType = model.EventType,
            DistanceKm = model.Distance,
            RouteName = model.Name + " Route",
            RouteDescription = model.Description
        };

        var success = await _api.PostAsync(
            "api/events",
            eventRequest);

        if (!success)
        {
            ModelState.AddModelError(
                "",
                "Could not create event. " + _api.LastError);

            return View(model);
        }

        TempData["Message"] = "Event created successfully.";

        return RedirectToAction(nameof(Dashboard));
    }

    // Upload an event banner.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBanner(int id, IFormFile file)
    {
        if (id <= 0)
        {
            TempData["Error"] = "Invalid event ID.";
            return RedirectToAction(nameof(Dashboard));
        }

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select an image.";
            return RedirectToAction(nameof(Dashboard));
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            TempData["Error"] = "The banner must not exceed 5 MB.";
            return RedirectToAction(nameof(Dashboard));
        }

        var allowedTypes = new[]
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            TempData["Error"] = "Please upload a JPG, PNG or WEBP image.";
            return RedirectToAction(nameof(Dashboard));
        }

        var success = await _api.UploadAsync(
            $"api/events/{id}/banner",
            file);

        TempData[success ? "Message" : "Error"] = success
            ? "Event banner uploaded successfully."
            : "Banner upload failed. Please check that you own this event.";

        return RedirectToAction(nameof(Dashboard));
    }

    // Display an event banner.
    [HttpGet]
    public async Task<IActionResult> EventBannerImage(int id)
    {
        var image = await _api.GetFileAsync(
            $"api/events/{id}/banner");

        if (image == null || image.Value.Data.Length == 0)
            return NotFound();

        return File(
            image.Value.Data,
            image.Value.ContentType);
    }

    // Delete an event.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        if (id <= 0)
        {
            TempData["Error"] = "Invalid event ID.";
            return RedirectToAction(nameof(Dashboard));
        }

        var success = await _api.DeleteAsync($"api/events/{id}");

        if (!success)
        {
            TempData["Error"] =
                "Could not delete the event. Please try again.";
        }
        else
        {
            TempData["Message"] = "Event deleted successfully.";
        }

        return RedirectToAction(nameof(Dashboard));
    }
}
