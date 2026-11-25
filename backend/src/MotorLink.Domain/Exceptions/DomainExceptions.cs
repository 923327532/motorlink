namespace MotorLink.Domain.Exceptions;

/// <summary>Base de las excepciones de negocio; el middleware de la API las traduce a ProblemDetails.</summary>
public abstract class DomainException(string message) : Exception(message);

public sealed class NotFoundException(string message) : DomainException(message);
public sealed class ConflictException(string message) : DomainException(message);
public sealed class ForbiddenException(string message) : DomainException(message);
public sealed class AuthenticationFailedException(string message) : DomainException(message);
public sealed class AccountLockedException(string message) : DomainException(message);

/// <summary>El plan contratado no permite la operación (límite o beneficio no incluido). HTTP 403.</summary>
public sealed class PlanLimitException(string message, string limit) : DomainException(message)
{
    public string Limit { get; } = limit;
}

/// <summary>Errores de reglas de negocio con detalle por campo.</summary>
public sealed class BusinessValidationException(IDictionary<string, string[]> errors)
    : DomainException("Una o más reglas de validación no se cumplieron.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public static BusinessValidationException For(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
