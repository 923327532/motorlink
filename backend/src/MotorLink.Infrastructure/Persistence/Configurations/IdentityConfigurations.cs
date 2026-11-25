using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MotorLink.Domain.Entities;

namespace MotorLink.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.ToTable("Users");
        // Unicidad de email a nivel de BD (Identity solo crea índice no único).
        b.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique()
            .HasFilter("[NormalizedEmail] IS NOT NULL");
        b.HasMany(u => u.UserRoles).WithOne(ur => ur.User).HasForeignKey(ur => ur.UserId).IsRequired();
        b.HasOne(u => u.Profile).WithOne(p => p.User).HasForeignKey<UserProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(u => u.BusinessProfile).WithOne(p => p.User).HasForeignKey<BusinessProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public static readonly Guid AdminRoleId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid UserRoleId = new("22222222-2222-2222-2222-222222222222");

    public void Configure(EntityTypeBuilder<ApplicationRole> b)
    {
        b.ToTable("Roles");
        b.HasMany<ApplicationUserRole>().WithOne(ur => ur.Role).HasForeignKey(ur => ur.RoleId).IsRequired();
        b.HasData(
            new ApplicationRole { Id = AdminRoleId, Name = RoleNames.Admin, NormalizedName = RoleNames.Admin, ConcurrencyStamp = "seed-admin-role" },
            new ApplicationRole { Id = UserRoleId, Name = RoleNames.User, NormalizedName = RoleNames.User, ConcurrencyStamp = "seed-user-role" });
    }
}

public sealed class ApplicationUserRoleConfiguration : IEntityTypeConfiguration<ApplicationUserRole>
{
    public void Configure(EntityTypeBuilder<ApplicationUserRole> b) => b.ToTable("UserRoles");
}

/// <summary>Tablas auxiliares de Identity con nombres coherentes.</summary>
public sealed class IdentityClaimsConfiguration :
    IEntityTypeConfiguration<IdentityUserClaim<Guid>>,
    IEntityTypeConfiguration<IdentityUserLogin<Guid>>,
    IEntityTypeConfiguration<IdentityRoleClaim<Guid>>,
    IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> b) => b.ToTable("UserClaims");
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> b) => b.ToTable("UserLogins");
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> b) => b.ToTable("RoleClaims");
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> b) => b.ToTable("UserTokens");
}

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.ToTable("UserProfiles");
        b.HasKey(p => p.UserId);
        b.Property(p => p.ProfileType).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
        b.Property(p => p.LastName).HasMaxLength(100).IsRequired();
        b.Property(p => p.PhoneNumber).HasMaxLength(20);
        b.Property(p => p.DocumentNumber).HasMaxLength(20);
        b.Property(p => p.Address).HasMaxLength(300);
        b.Property(p => p.AvatarUrl).HasMaxLength(500);
        b.HasOne(p => p.District).WithMany().HasForeignKey(p => p.DistrictId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => p.DocumentNumber);
    }
}

public sealed class BusinessProfileConfiguration : IEntityTypeConfiguration<BusinessProfile>
{
    public void Configure(EntityTypeBuilder<BusinessProfile> b)
    {
        b.ToTable("BusinessProfiles");
        b.HasKey(p => p.Id);
        b.Property(p => p.BusinessName).HasMaxLength(200).IsRequired();
        b.Property(p => p.Ruc).HasMaxLength(11);
        b.Property(p => p.Description).HasMaxLength(1000);
        b.Property(p => p.Website).HasMaxLength(300);
        b.Property(p => p.PhoneNumber).HasMaxLength(20);
        b.Property(p => p.Address).HasMaxLength(300);
        b.HasIndex(p => p.UserId).IsUnique();
        b.HasIndex(p => p.Ruc).IsUnique().HasFilter("[Ruc] IS NOT NULL");
        b.HasOne(p => p.District).WithMany().HasForeignKey(p => p.DistrictId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens");
        b.HasKey(t => t.Id);
        b.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);
        b.Property(t => t.CreatedByIp).HasMaxLength(45);
        b.Property(t => t.RevocationReason).HasConversion<string>().HasMaxLength(30);
        b.Ignore(t => t.IsRevoked);
        b.HasIndex(t => t.TokenHash).IsUnique();
        b.HasIndex(t => t.FamilyId);
        b.HasIndex(t => new { t.UserId, t.ExpiresAt });
        b.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
