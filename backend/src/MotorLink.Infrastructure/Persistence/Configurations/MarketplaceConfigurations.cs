using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MotorLink.Domain.Entities;

namespace MotorLink.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> b)
    {
        b.ToTable("Vehicles", t =>
        {
            t.HasCheckConstraint("CK_Vehicles_Years", "[ManufactureYear] BETWEEN 1950 AND 2100 AND [ModelYear] >= [ManufactureYear]");
            t.HasCheckConstraint("CK_Vehicles_Mileage", "[Mileage] >= 0 AND [PreviousOwners] >= 0");
        });
        b.Property(x => x.Condition).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.FuelType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Transmission).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Traction).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Version).HasMaxLength(80);
        b.Property(x => x.Plate).HasMaxLength(12);
        b.Property(x => x.Color).HasMaxLength(40);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.LegalOwnerName).HasMaxLength(150);
        b.Property(x => x.LegalOwnerRelationship).HasMaxLength(80);

        // Placa única entre vehículos activos (los eliminados lógicamente no bloquean su reutilización).
        b.HasIndex(x => x.Plate).IsUnique().HasFilter("[Plate] IS NOT NULL AND [IsDeleted] = 0").HasDatabaseName("UX_Vehicles_Plate_Active");
        b.HasIndex(x => x.OwnerId);
        b.HasIndex(x => x.ModelId);
        b.HasIndex(x => new { x.VehicleTypeId, x.ModelYear });
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.VehicleType).WithMany().HasForeignKey(x => x.VehicleTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Model).WithMany().HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VehicleImageConfiguration : IEntityTypeConfiguration<VehicleImage>
{
    public void Configure(EntityTypeBuilder<VehicleImage> b)
    {
        b.ToTable("VehicleImages");
        b.Property(x => x.Url).HasMaxLength(500).IsRequired();
        b.Property(x => x.ProviderPublicId).HasMaxLength(255).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(30).IsRequired();
        b.HasIndex(x => new { x.VehicleId, x.SortOrder });
        b.HasIndex(x => x.ProviderPublicId);
        b.HasOne(x => x.Vehicle).WithMany(v => v.Images).HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class OrphanedImageConfiguration : IEntityTypeConfiguration<OrphanedImage>
{
    public void Configure(EntityTypeBuilder<OrphanedImage> b)
    {
        b.ToTable("OrphanedImages");
        b.Property(x => x.ProviderPublicId).HasMaxLength(255).IsRequired();
        b.Property(x => x.LastError).HasMaxLength(400);
        b.HasIndex(x => new { x.Attempts, x.CreatedAt });
    }
}

public sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> b)
    {
        b.ToTable("Listings");
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.AdvertiserType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.BusinessName).HasMaxLength(200);
        b.Property(x => x.PhoneNumber).HasMaxLength(20);
        b.Property(x => x.WhatsAppNumber).HasMaxLength(20);
        b.Property(x => x.ContactHours).HasMaxLength(100);
        b.Property(x => x.LocationReference).HasMaxLength(300);
        b.Property(x => x.AdditionalConditions).HasMaxLength(1000);
        b.Property(x => x.RejectionReason).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => new { x.Status, x.PublishedAt });
        b.HasIndex(x => new { x.SellerId, x.Status });
        b.HasIndex(x => new { x.DepartmentId, x.ProvinceId, x.DistrictId });
        // Un vehículo tiene a lo sumo una publicación vigente (las eliminadas lógicamente no cuentan).
        b.HasIndex(x => x.VehicleId).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UX_Listings_Vehicle_Active");

        b.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Seller).WithMany().HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Province).WithMany().HasForeignKey(x => x.ProvinceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.District).WithMany().HasForeignKey(x => x.DistrictId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ListingOperationConfiguration : IEntityTypeConfiguration<ListingOperation>
{
    public void Configure(EntityTypeBuilder<ListingOperation> b)
    {
        b.ToTable("ListingOperations", t => t.HasCheckConstraint("CK_ListingOperations_Prices",
            "[ReferencePrice] >= 0 AND ([SalePrice] IS NULL OR [SalePrice] > 0) AND ([DailyRate] IS NULL OR [DailyRate] > 0) " +
            "AND ([WeeklyRate] IS NULL OR [WeeklyRate] > 0) AND ([MonthlyRate] IS NULL OR [MonthlyRate] > 0) AND ([Guarantee] IS NULL OR [Guarantee] >= 0)"));
        b.Property(x => x.OperationType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Currency).HasConversion<string>().HasMaxLength(3);
        b.Property(x => x.AdditionalConditions).HasMaxLength(1000);
        b.HasIndex(x => new { x.ListingId, x.OperationType }).IsUnique();
        b.HasIndex(x => new { x.OperationType, x.Currency, x.ReferencePrice });
        b.HasOne(x => x.Listing).WithMany(l => l.Operations).HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.WorkRent).WithOne(w => w.ListingOperation).HasForeignKey<WorkRentCondition>(w => w.ListingOperationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkRentConditionConfiguration : IEntityTypeConfiguration<WorkRentCondition>
{
    public void Configure(EntityTypeBuilder<WorkRentCondition> b)
    {
        b.ToTable("WorkRentConditions");
        b.HasKey(x => x.ListingOperationId);
        b.Property(x => x.AllowedUse).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Shift).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.FuelPaidBy).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.MaintenancePaidBy).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.InsurancePaidBy).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.LicenseType).HasMaxLength(30);
        b.Property(x => x.WorkZone).HasMaxLength(200);
        b.Property(x => x.DriverRequirements).HasMaxLength(1000);
        b.HasIndex(x => x.AllowedUse);
    }
}

public sealed class ListingStatusHistoryConfiguration : IEntityTypeConfiguration<ListingStatusHistory>
{
    public void Configure(EntityTypeBuilder<ListingStatusHistory> b)
    {
        b.ToTable("ListingStatusHistory");
        b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.OperationType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => new { x.ListingId, x.ChangedAt });
        b.HasIndex(x => new { x.ToStatus, x.ChangedAt });
        b.HasOne<Listing>().WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> b)
    {
        b.ToTable("Favorites");
        b.HasKey(x => new { x.UserId, x.ListingId });
        b.HasIndex(x => x.ListingId);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NegotiationConfiguration : IEntityTypeConfiguration<Negotiation>
{
    public void Configure(EntityTypeBuilder<Negotiation> b)
    {
        b.ToTable("Negotiations");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Currency).HasConversion<string>().HasMaxLength(3);
        b.Property(x => x.Periodicity).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.ListingId);
        b.HasIndex(x => new { x.BuyerId, x.Status });
        b.HasIndex(x => new { x.SellerId, x.Status });
        b.HasIndex(x => new { x.Status, x.ExpiresAt });
        // Un comprador solo puede tener una negociación abierta por modalidad.
        b.HasIndex(x => new { x.ListingOperationId, x.BuyerId }).IsUnique()
            .HasFilter("[Status] IN ('Pending','CounterOffer')").HasDatabaseName("UX_Negotiations_OpenPerBuyer");
        b.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ListingOperation).WithMany().HasForeignKey(x => x.ListingOperationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Buyer).WithMany().HasForeignKey(x => x.BuyerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Seller).WithMany().HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NegotiationOfferConfiguration : IEntityTypeConfiguration<NegotiationOffer>
{
    public void Configure(EntityTypeBuilder<NegotiationOffer> b)
    {
        b.ToTable("NegotiationOffers", t => t.HasCheckConstraint("CK_NegotiationOffers_Amount", "[Amount] > 0"));
        b.Property(x => x.Message).HasMaxLength(1000);
        b.Property(x => x.Currency).HasConversion<string>().HasMaxLength(3);
        b.Property(x => x.Periodicity).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.NegotiationId, x.Sequence }).IsUnique();
        b.HasOne(x => x.Negotiation).WithMany(n => n.Offers).HasForeignKey(x => x.NegotiationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.OfferedBy).WithMany().HasForeignKey(x => x.OfferedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> b)
    {
        b.ToTable("Subscriptions");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.PaymentMode).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.UserId, x.Status });
        b.HasIndex(x => new { x.Status, x.EndsAt });
        // A lo sumo una suscripción ACTIVA por usuario (garantía final frente a concurrencia).
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("[Status] = 'Active'").HasDatabaseName("UX_Subscriptions_ActivePerUser");
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments", t => t.HasCheckConstraint("CK_Payments_Amount", "[Amount] >= 0"));
        b.Property(x => x.Currency).HasConversion<string>().HasMaxLength(3);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Provider).HasMaxLength(50);
        b.Property(x => x.ExternalReference).HasMaxLength(100);
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.SubscriptionId);
        b.HasIndex(x => new { x.IsSimulated, x.Status });
        b.HasIndex(x => new { x.Provider, x.ExternalReference }).IsUnique().HasFilter("[ExternalReference] IS NOT NULL");
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.Property(x => x.Type).HasMaxLength(50).IsRequired();
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        b.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ListingViewConfiguration : IEntityTypeConfiguration<ListingView>
{
    public void Configure(EntityTypeBuilder<ListingView> b)
    {
        b.ToTable("ListingViews");
        b.HasIndex(x => new { x.ListingId, x.ViewedAt });
        b.HasIndex(x => new { x.ViewerId, x.ListingId, x.ViewedAt });
        b.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ContactEventConfiguration : IEntityTypeConfiguration<ContactEvent>
{
    public void Configure(EntityTypeBuilder<ContactEvent> b)
    {
        b.ToTable("ContactEvents");
        b.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.ListingId, x.CreatedAt });
        b.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityName).HasMaxLength(100);
        b.Property(x => x.EntityId).HasMaxLength(64);
        b.Property(x => x.Details).HasMaxLength(1000);
        b.Property(x => x.IpAddress).HasMaxLength(45);
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => x.Action);
    }
}
