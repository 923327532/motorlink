using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MotorLink.Web.Extensions;
using MotorLink.Web.Services.Api;

namespace MotorLink.Web.Filters;

/// <summary>
/// Manejo central de los errores de la API:
///  401 → cierra la sesión local y redirige al login (o responde 401 JSON si es una llamada AJAX);
///  403 (rol/política) → página de acceso denegado;
///  fallo al cargar datos imprescindibles → página de error amable con la acción de reintento.
/// </summary>
public sealed class ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        var http = context.HttpContext;
        var ajax = http.Request.IsAjax();

        switch (context.Exception)
        {
            case ApiUnauthorizedException:
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Result = ajax
                    ? new JsonResult(new { ok = false, message = "Tu sesión expiró. Inicia sesión nuevamente.", login = "/ingresar" }) { StatusCode = 401 }
                    : new RedirectResult($"/ingresar?returnUrl={Uri.EscapeDataString(http.Request.Path + http.Request.QueryString)}&expired=1");
                break;

            case ApiForbiddenException:
                context.Result = ajax
                    ? new JsonResult(new { ok = false, message = "No tienes permisos para esta acción." }) { StatusCode = 403 }
                    : new RedirectResult("/acceso-denegado");
                break;

            case ApiPageException page:
                logger.LogWarning("La API respondió {Status} en {Path}: {Message}", page.Error.Status, http.Request.Path, page.Error.Message);
                if (page.Error.Status == 404)
                    context.Result = new NotFoundResult();
                else if (ajax)
                    context.Result = new JsonResult(new { ok = false, message = page.Error.Message }) { StatusCode = page.Error.Status is >= 400 and < 600 ? page.Error.Status : 502 };
                else
                    context.Result = new ViewResult { ViewName = "~/Views/Shared/ApiError.cshtml", ViewData = ViewData(context, page.Error), StatusCode = page.Error.Status == 503 ? 503 : 502 };
                break;

            default:
                return;
        }
        context.ExceptionHandled = true;
    }

    private static Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary ViewData(ExceptionContext c, ApiError e)
    {
        var vd = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), c.ModelState) { Model = e };
        return vd;
    }
}
