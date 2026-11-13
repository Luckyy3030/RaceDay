
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using System.Globalization;
using System.Net.Http.Json;

namespace RaceDay.Mvc.Controllers;

public class AccountController : Controller
{
    private readonly HttpClient _client;

    public AccountController(IHttpClientFactory httpClientFactory)
    {
        _client = httpClientFactory.CreateClient("RaceDayApi");
    }

    // Display login page.
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    // Process login.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var response = await _client.PostAsJsonAsync(
            "api/auth/login",
            model);

        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(
                "",
                "Invalid email or password.");

            return View(model);
        }

        var result =
            await response.Content.ReadFromJsonAsync<LoginResponse>();

        if (result == null ||
            string.IsNullOrWhiteSpace(result.Token))
        {
            ModelState.AddModelError(
                "",
                "Login could not be completed.");

            return View(model);
        }

        // Save login information in session.
        HttpContext.Session.SetString("Token", result.Token);
        HttpContext.Session.SetString("Role", result.Role ?? "");
        HttpContext.Session.SetString("UserId", result.UserId.ToString());
        HttpContext.Session.SetString("FirstName", result.FirstName ?? "");

        // Redirect users according to their roles.
        if (string.Equals(
            result.Role,
            "Organiser",
            StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(
                "Dashboard",
                "Organiser");
        }

        if (string.Equals(
            result.Role,
            "Participant",
            StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(
                "Dashboard",
                "Participant");
        }

        HttpContext.Session.Clear();

        ModelState.AddModelError(
            "",
            "Your account has an unrecognised role.");

        return View(model);
    }

    // Display registration page.
    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    // Process registration.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var registrationRequest = new
        {
            model.FirstName,
            model.LastName,
            model.Email,
            model.Password,
            model.Role,
            model.OrganisationName,

            DateOfBirth = model.DateOfBirth?.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture),

            model.EmergencyContact
        };

        var response = await _client.PostAsJsonAsync(
            "api/auth/register",
            registrationRequest);

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage =
                await response.Content.ReadAsStringAsync();

            ModelState.AddModelError(
                "",
                "Registration failed. Please check your information. " +
                errorMessage);

            return View(model);
        }

        TempData["Message"] =
            "Registration successful. Please log in.";

        return RedirectToAction("Login");
    }

    // Log out.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();

        return RedirectToAction("Index", "Home");
    }
}

// API login response.
public class LoginResponse
{
    public string Token { get; set; } = "";

    public int UserId { get; set; }

    public string FirstName { get; set; } = "";

    public string LastName { get; set; } = "";

    public string Email { get; set; } = "";

    public string Role { get; set; } = "";
}
