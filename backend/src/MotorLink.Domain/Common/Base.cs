namespace MotorLink.Domain.Common;

/// <summary>Entidad con marcas de auditoría. Todas las fechas se almacenan en UTC.</summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}

/// <summary>Entidad con eliminación lógica.</summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}

public abstract class AuditableEntity<TKey> : IAuditable
{
    public TKey Id { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public abstract class SoftDeletableEntity<TKey> : AuditableEntity<TKey>, ISoftDelete
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
