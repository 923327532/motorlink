using MotorLink.Domain.Common;

namespace MotorLink.Domain.Entities;

// ---- Ubicaciones (UBIGEO/INEI) ----
public class Department
{
    public int Id { get; set; }
    /// <summary>Código UBIGEO de 2 dígitos.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Province> Provinces { get; set; } = new List<Province>();
}

public class Province
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    /// <summary>Código UBIGEO de 4 dígitos.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Department Department { get; set; } = null!;
    public ICollection<District> Districts { get; set; } = new List<District>();
}

public class District
{
    public int Id { get; set; }
    public int ProvinceId { get; set; }
    /// <summary>Código UBIGEO de 6 dígitos.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Province Province { get; set; } = null!;
}

// ---- Catálogo de vehículos ----
public class VehicleType
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class Brand
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<VehicleModel> Models { get; set; } = new List<VehicleModel>();
}

/// <summary>Tabla Models (modelo de vehículo de una marca).</summary>
public class VehicleModel
{
    public int Id { get; set; }
    public int BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Brand Brand { get; set; } = null!;
    /// <summary>Tipos de vehículo con los que el modelo es compatible.</summary>
    public ICollection<VehicleModelType> CompatibleTypes { get; set; } = new List<VehicleModelType>();
}

public class VehicleModelType
{
    public int ModelId { get; set; }
    public int VehicleTypeId { get; set; }
    public VehicleModel Model { get; set; } = null!;
    public VehicleType VehicleType { get; set; } = null!;
}

// ---- Planes SaaS (todos los límites y beneficios viven en BD) ----
public class Plan
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public int MaxActiveListings { get; set; }
    public int MaxPhotosPerVehicle { get; set; }
    public int MaxFeaturedListings { get; set; }
    public bool HasAdvancedStats { get; set; }
    public bool HasBusinessProfile { get; set; }
    public bool CanExportReports { get; set; }
    public bool HasPrioritySupport { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
