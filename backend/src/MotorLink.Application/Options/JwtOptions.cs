namespace MotorLink.Application.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "MotorLink.API";
    public string Audience { get; set; } = "MotorLink.Web";
    /// <summary>Clave simétrica (mínimo 32 caracteres). Debe venir de variables de entorno o secretos.</summary>
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
