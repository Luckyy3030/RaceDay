using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Mvc.Filters;

public class SessionAuthorizeAttribute : ActionFilterAttribute
{
    public string? RequiredRole { get; set; }

    public override void OnActionExecuting(
        ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;

        var token = session.GetString("Token");
        var role = session.GetString("Role");

        if (string.IsNullOrWhiteSpace(token))
        {
            context.Result = new RedirectToActionResult(
                "Login", "Account", null);
            return;
        }

        if (RequiredRole != null && role != RequiredRole)
        {
            context.Result = new RedirectToActionResult(
                "AccessDenied", "Home", null);
        }
    }
}
