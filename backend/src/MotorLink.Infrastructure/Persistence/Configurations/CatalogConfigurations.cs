using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MotorLink.Domain.Entities;
using MotorLink.Infrastructure.Persistence.Seed;

namespace MotorLink.Infrastructure.Persistence.Configurations;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("Departments");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasMaxLength(2).IsFixedLength().IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasData(GeographySeed.Departments);
    }
}

public sealed class ProvinceConfiguration : IEntityTypeConfiguration<Province>
{
    public void Configure(EntityTypeBuilder<Province> b)
    {
        b.ToTable("Provinces");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasMaxLength(4).IsFixedLength().IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.DepartmentId);
        b.HasOne(x => x.Department).WithMany(d => d.Provinces).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasData(GeographySeed.Provinces);
    }
}

public sealed class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> b)
    {
        b.ToTable("Districts");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasMaxLength(6).IsFixedLength().IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.ProvinceId);
        b.HasOne(x => x.Province).WithMany(p => p.Districts).HasForeignKey(x => x.ProvinceId).OnDelete(DeleteBehavior.Restrict);
        b.HasData(GeographySeed.Districts);
    }
}

public sealed class VehicleTypeConfiguration : IEntityTypeConfiguration<VehicleType>
{
    public void Configure(EntityTypeBuilder<VehicleType> b)
    {
        b.ToTable("VehicleTypes");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.Property(x => x.Name).HasMaxLength(60).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasData(VehicleCatalogSeed.VehicleTypes);
    }
}

public sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> b)
    {
        b.ToTable("Brands");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(60).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.HasData(VehicleCatalogSeed.Brands);
    }
}

public sealed class VehicleModelConfiguration : IEntityTypeConfiguration<VehicleModel>
{
    public void Configure(EntityTypeBuilder<VehicleModel> b)
    {
        b.ToTable("Models");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.HasIndex(x => new { x.BrandId, x.Name }).IsUnique();
        b.HasOne(x => x.Brand).WithMany(br => br.Models).HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        b.HasData(VehicleCatalogSeed.Models);
    }
}

/// <summary>Compatibilidad modelo ↔ tipo de vehículo (N:M).</summary>
public sealed class VehicleModelTypeConfiguration : IEntityTypeConfiguration<VehicleModelType>
{
    public void Configure(EntityTypeBuilder<VehicleModelType> b)
    {
        b.ToTable("ModelVehicleTypes");
        b.HasKey(x => new { x.ModelId, x.VehicleTypeId });
        b.HasIndex(x => x.VehicleTypeId);
        b.HasOne(x => x.Model).WithMany(m => m.CompatibleTypes).HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.VehicleType).WithMany().HasForeignKey(x => x.VehicleTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasData(VehicleCatalogSeed.ModelTypes);
    }
}

public sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> b)
    {
        b.ToTable("Plans", t =>
        {
            t.HasCheckConstraint("CK_Plans_Price", "[MonthlyPrice] >= 0");
            t.HasCheckConstraint("CK_Plans_Limits", "[MaxActiveListings] >= 0 AND [MaxPhotosPerVehicle] >= 0 AND [MaxFeaturedListings] >= 0");
        });
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.Property(x => x.Name).HasMaxLength(60).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasData(
            P(1, "FREE", "Gratis", 0m, 2, 5, 0, false, false, false, false, 1),
            P(2, "BASIC", "Básico", 19m, 5, 10, 1, false, false, false, false, 2),
            P(3, "PROFESSIONAL", "Profesional", 39m, 15, 15, 3, true, true, false, false, 3),
            P(4, "BUSINESS", "Business", 79m, 40, 20, 8, true, true, true, false, 4),
            P(5, "ENTERPRISE", "Enterprise", 149m, 100, 25, 15, true, true, true, true, 5));
    }

    private static Plan P(int id, string code, string name, decimal price, int listings, int photos, int featured,
        bool adv, bool biz, bool export, bool support, int order) => new()
    {
        Id = id, Code = code, Name = name, MonthlyPrice = price, MaxActiveListings = listings, MaxPhotosPerVehicle = photos,
        MaxFeaturedListings = featured, HasAdvancedStats = adv, HasBusinessProfile = biz, CanExportReports = export,
        HasPrioritySupport = support, SortOrder = order, IsActive = true
    };
}
