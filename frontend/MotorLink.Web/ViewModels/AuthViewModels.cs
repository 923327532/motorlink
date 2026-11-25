using System.ComponentModel.DataAnnotations;

namespace MotorLink.Web.ViewModels;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Ingresa tu correo electrónico."), EmailAddress(ErrorMessage = "El correo no tiene un formato válido."), Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu contraseña."), DataType(DataType.Password), Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class RegisterViewModel : IValidatableObject
{
    [Display(Name = "Tipo de cuenta")]
    public string ProfileType { get; set; } = "PERSONAL";

    [StringLength(200), Display(Name = "Nombre comercial")]
    public string? BusinessName { get; set; }

    [Required(ErrorMessage = "Ingresa tus nombres."), StringLength(100), Display(Name = "Nombres")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tus apellidos."), StringLength(100), Display(Name = "Apellidos")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu correo electrónico."), EmailAddress(ErrorMessage = "El correo no tiene un formato válido."), Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "El teléfono no es válido."), StringLength(20), Display(Name = "Teléfono")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Crea una contraseña."), StringLength(128, MinimumLength = 8, ErrorMessage = "Mínimo 8 caracteres."), DataType(DataType.Password), Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirma tu contraseña."), DataType(DataType.Password), Display(Name = "Confirmar contraseña"),
     Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [MustBeTrue(ErrorMessage = "Debes aceptar los términos y la política de privacidad.")]
    public bool AcceptTerms { get; set; }

    public string? ReturnUrl { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (ProfileType == "BUSINESS" && string.IsNullOrWhiteSpace(BusinessName))
            yield return new ValidationResult("El nombre comercial es obligatorio para cuentas empresariales.", [nameof(BusinessName)]);
    }
}

public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Ingresa tu contraseña actual."), DataType(DataType.Password), Display(Name = "Contraseña actual")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa la nueva contraseña."), StringLength(128, MinimumLength = 8, ErrorMessage = "Mínimo 8 caracteres."), DataType(DataType.Password), Display(Name = "Nueva contraseña")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirma la nueva contraseña."), DataType(DataType.Password), Display(Name = "Confirmar nueva contraseña"),
     Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public sealed class ProfileViewModel : IValidatableObject
{
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tus nombres."), StringLength(100), Display(Name = "Nombres")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tus apellidos."), StringLength(100), Display(Name = "Apellidos")]
    public string LastName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "El teléfono no es válido."), StringLength(20), Display(Name = "Teléfono")]
    public string? PhoneNumber { get; set; }

    [StringLength(20), Display(Name = "Documento de identidad")]
    public string? DocumentNumber { get; set; }

    [Display(Name = "Tipo de cuenta")]
    public string ProfileType { get; set; } = "PERSONAL";

    [StringLength(200), Display(Name = "Nombre comercial")]
    public string? BusinessName { get; set; }

    [RegularExpression(@"^\d{11}$", ErrorMessage = "El RUC debe tener 11 dígitos."), Display(Name = "RUC")]
    public string? Ruc { get; set; }

    [StringLength(1000), Display(Name = "Descripción del negocio")]
    public string? BusinessDescription { get; set; }

    [Url(ErrorMessage = "La URL no es válida."), StringLength(300), Display(Name = "Sitio web")]
    public string? Website { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (ProfileType == "BUSINESS" && string.IsNullOrWhiteSpace(BusinessName))
            yield return new ValidationResult("El nombre comercial es obligatorio para cuentas empresariales.", [nameof(BusinessName)]);
    }
}

/// <summary>El valor booleano debe ser true (validación en servidor; el campo es una casilla de aceptación).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MustBeTrueAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is true;
}
