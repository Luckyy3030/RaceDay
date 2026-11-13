using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Mvc.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult AccessDenied()
    {
        return View();
    }

    public IActionResult Error()
    {
        return View();
    }
}