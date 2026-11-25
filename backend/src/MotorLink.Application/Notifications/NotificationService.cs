using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Common;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;

namespace MotorLink.Application.Notifications;

public sealed record NotificationDto(Guid Id, string Type, string Title, string Message, Guid? RelatedEntityId, bool IsRead, DateTime CreatedAt);

public interface INotificationService
{
    /// <summary>Agrega la notificación al contexto; se persiste con el SaveChanges del caso de uso.</summary>
    void Notify(Guid userId, string type, string title, string message, Guid? relatedEntityId = null);

    Task<PagedResult<NotificationDto>> ListAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct);
    Task<int> UnreadCountAsync(Guid userId, CancellationToken ct);
    Task MarkReadAsync(Guid userId, Guid id, CancellationToken ct);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct);
}

public sealed class NotificationService(IApplicationDbContext db, TimeProvider clock) : INotificationService
{
    public void Notify(Guid userId, string type, string title, string message, Guid? relatedEntityId = null) =>
        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(), UserId = userId, Type = type, Title = title, Message = message, RelatedEntityId = relatedEntityId
        });

    public Task<PagedResult<NotificationDto>> ListAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct) =>
        db.Notifications.AsNoTracking().Where(n => n.UserId == userId && (!unreadOnly || !n.IsRead))
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Message, n.RelatedEntityId, n.IsRead, n.CreatedAt))
            .ToPagedAsync(page, pageSize, ct);

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken ct) =>
        db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task MarkReadAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new NotFoundException("Notificación no encontrada.");
        if (n.IsRead) return;
        n.IsRead = true; n.ReadAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var n in await db.Notifications.Where(x => x.UserId == userId && !x.IsRead).ToListAsync(ct))
        { n.IsRead = true; n.ReadAt = now; }
        await db.SaveChangesAsync(ct);
    }
}
