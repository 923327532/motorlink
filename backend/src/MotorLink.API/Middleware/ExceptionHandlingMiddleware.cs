using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MotorLink.Domain.Exceptions;

namespace MotorLink.API.Middleware;

/// <summary>Traduce excepciones a respuestas ProblemDetails (RFC 9457) sin filtrar detalles internos.</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499; // el cliente canceló la solicitud
        }
        catch (Exception ex)
        {
            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        if (context.Response.HasStarted)
        {
            logger.LogError(ex, "Error después de iniciar la respuesta en {Path}", context.Request.Path);
            return;
        }

        ProblemDetails problem;
        switch (ex)
        {
            case BusinessValidationException v:
                problem = new ValidationProblemDetails(v.Errors) { Status = 400, Title = "Error de validación" };
                break;
            case AuthenticationFailedException:
                problem = Create(401, "No autenticado", ex.Message);
                break;
            case AccountLockedException:
                problem = Create(423, "Cuenta bloqueada", ex.Message);
                break;
            case PlanLimitException pl:
                problem = Create(403, "Límite del plan", pl.Message);
                problem.Extensions["code"] = pl.Limit;
                break;
            case ForbiddenException:
                problem = Create(403, "Acceso denegado", ex.Message);
                break;
            case NotFoundException:
                problem = Create(404, "Recurso no encontrado", ex.Message);
                break;
            case ConflictException:
                problem = Create(409, "Conflicto", ex.Message);
                break;
            case DbUpdateConcurrencyException:
                // Concurrencia optimista: otra solicitud modificó el mismo registro.
                problem = Create(409, "Conflicto de concurrencia", "El recurso fue modificado por otra operación. Recarga e inténtalo de nuevo.");
                break;
            case DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } }:
                problem = Create(409, "Registro duplicado", "Ya existe un registro con esos datos.");
                break;
            case DbUpdateException { InnerException: SqlException { Number: 547 } }:
                problem = Create(409, "Operación no permitida", "El registro está relacionado con otros datos o referencia datos inexistentes.");
                break;
            case BadHttpRequestException bad:
                problem = Create(400, "Solicitud inválida", bad.Message);
                break;
            default:
                logger.LogError(ex, "Error no controlado en {Method} {Path}", context.Request.Method, context.Request.Path);
                problem = Create(500, "Error interno", "Ocurrió un error inesperado.");
                break;
        }

        if (problem.Status < 500)
            logger.LogInformation("Solicitud rechazada ({Status}) {ExceptionType} en {Path}",
                problem.Status, ex.GetType().Name, context.Request.Path);

        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        context.Response.StatusCode = problem.Status ?? 500;
        // Se serializa por tipo en tiempo de ejecución para conservar 'errors' de ValidationProblemDetails.
        await context.Response.WriteAsJsonAsync(problem, problem.GetType(), (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }

    private static ProblemDetails Create(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };
}
