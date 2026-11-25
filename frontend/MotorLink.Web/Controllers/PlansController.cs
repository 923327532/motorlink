using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MotorLink.Web.Services.Api;

namespace MotorLink.Web.Controllers;

[AllowAnonymous]
public sealed class PlansController(IBillingApi billing) : Controller
{
    [HttpGet("planes")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var plans = (await billing.PlansAsync(ct)).Data();
        string? current = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var c = await billing.CurrentAsync(ct);
            if (c.Success) current = c.Value!.Plan.Code;
        }
        ViewData["Nav"] = "plans";
        ViewData["CurrentCode"] = current;
        return View(plans);
    }
}
