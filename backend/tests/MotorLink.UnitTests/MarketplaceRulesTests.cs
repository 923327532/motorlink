using MotorLink.Application.Dashboard;
using MotorLink.Application.Listings;
using MotorLink.Application.Vehicles;
using MotorLink.Domain.Common;
using MotorLink.Domain.Rules;

namespace MotorLink.UnitTests;

public class ListingStateMachineTests
{
    [Theory]
    [InlineData(ListingStatus.Draft, ListingStatus.PendingReview)]
    [InlineData(ListingStatus.PendingReview, ListingStatus.Published)]
    [InlineData(ListingStatus.PendingReview, ListingStatus.Rejected)]
    [InlineData(ListingStatus.Rejected, ListingStatus.Draft)]
    [InlineData(ListingStatus.Published, ListingStatus.Reserved)]
    [InlineData(ListingStatus.Published, ListingStatus.Rented)]
    [InlineData(ListingStatus.Published, ListingStatus.Sold)]
    [InlineData(ListingStatus.Published, ListingStatus.Paused)]
    [InlineData(ListingStatus.Reserved, ListingStatus.Published)]
    [InlineData(ListingStatus.Rented, ListingStatus.Published)]
    [InlineData(ListingStatus.Paused, ListingStatus.Published)]
    [InlineData(ListingStatus.Sold, ListingStatus.Archived)]
    [InlineData(ListingStatus.Archived, ListingStatus.Draft)]
    public void Valid_transitions_are_allowed(ListingStatus from, ListingStatus to) =>
        Assert.True(ListingStateMachine.CanTransition(from, to));

    [Theory]
    [InlineData(ListingStatus.Draft, ListingStatus.Published)]          // sin moderación
    [InlineData(ListingStatus.Draft, ListingStatus.Sold)]
    [InlineData(ListingStatus.Sold, ListingStatus.Published)]           // vendido es terminal
    [InlineData(ListingStatus.Sold, ListingStatus.Rented)]
    [InlineData(ListingStatus.Rejected, ListingStatus.Published)]
    [InlineData(ListingStatus.Paused, ListingStatus.Sold)]
    [InlineData(ListingStatus.Archived, ListingStatus.Published)]
    [InlineData(ListingStatus.Published, ListingStatus.Draft)]
    public void Invalid_transitions_are_rejected(ListingStatus from, ListingStatus to) =>
        Assert.False(ListingStateMachine.CanTransition(from, to));

    [Fact]
    public void Every_status_other_than_archived_can_eventually_be_archived_and_no_state_transitions_to_itself()
    {
        foreach (var from in Enum.GetValues<ListingStatus>())
            Assert.False(ListingStateMachine.CanTransition(from, from));
        Assert.True(ListingStateMachine.CanTransition(ListingStatus.Draft, ListingStatus.Archived));
    }

    [Fact]
    public void Only_review_outcomes_require_a_moderator()
    {
        Assert.True(ListingStateMachine.RequiresModerator(ListingStatus.PendingReview, ListingStatus.Published));
        Assert.True(ListingStateMachine.RequiresModerator(ListingStatus.PendingReview, ListingStatus.Rejected));
        Assert.False(ListingStateMachine.RequiresModerator(ListingStatus.Paused, ListingStatus.Published));
        Assert.False(ListingStateMachine.RequiresModerator(ListingStatus.Published, ListingStatus.Sold));
    }

    [Fact]
    public void Plan_limit_counts_pending_published_reserved_and_rented_but_not_paused_or_draft()
    {
        Assert.Contains(ListingStatus.PendingReview, ListingStateMachine.CountsTowardLimit);
        Assert.Contains(ListingStatus.Rented, ListingStateMachine.CountsTowardLimit);
        Assert.DoesNotContain(ListingStatus.Paused, ListingStateMachine.CountsTowardLimit);
        Assert.DoesNotContain(ListingStatus.Draft, ListingStateMachine.CountsTowardLimit);
        Assert.DoesNotContain(ListingStatus.Sold, ListingStateMachine.PublicVisible);
    }
}

public class VehicleRulesTests
{
    private static VehicleRequest Car(Action<Dictionary<string, object?>>? tweak = null) => new()
    {
        VehicleTypeId = 1, BrandId = 1, ModelId = 1, ManufactureYear = 2020, ModelYear = 2020, Plate = "ABC123", Mileage = 1000,
        Condition = VehicleCondition.Used, PreviousOwners = 1, FuelType = FuelType.Gasoline, Transmission = TransmissionType.Manual,
        EngineDisplacementCc = 1500, Doors = 4, Seats = 5
    };

    [Theory]
    [InlineData("abc-123", "ABC123")]
    [InlineData(" ab 12 c ", "AB12C")]
    [InlineData("v1a-234", "V1A234")]
    public void Plate_is_normalized(string raw, string expected) => Assert.Equal(expected, PlateNormalizer.Normalize(raw));

    [Fact]
    public void Blank_plate_is_null_and_masking_hides_all_but_two_characters()
    {
        Assert.Null(PlateNormalizer.Normalize("  "));
        Assert.Null(PlateNormalizer.Normalize(null));
        Assert.Equal("AB****", PlateNormalizer.Mask("ABC123"));
        Assert.Null(PlateNormalizer.Mask(null));
        Assert.DoesNotContain("123", PlateNormalizer.Mask("ABC123"));
    }

    [Theory]
    [InlineData("ABC123", true)]
    [InlineData("AB12", false)]
    [InlineData("ABCDEFGH", false)]
    public void Plate_format(string plate, bool valid) => Assert.Equal(valid, PlateNormalizer.IsValid(plate));

    [Fact]
    public void A_valid_car_has_no_errors() =>
        Assert.Empty(VehicleSpecValidator.Validate(Car(), VehicleTypeCodes.Car, "ABC123", 2026));

    [Fact]
    public void Used_vehicles_need_a_plate_but_new_ones_do_not()
    {
        Assert.Contains("Plate", VehicleSpecValidator.Validate(Car(), VehicleTypeCodes.Car, null, 2026).Keys);
        var fresh = Car() with { Condition = VehicleCondition.New, PreviousOwners = 0, Mileage = 20, ManufactureYear = 2026, ModelYear = 2026 };
        Assert.Empty(VehicleSpecValidator.Validate(fresh, VehicleTypeCodes.Car, null, 2026));
        Assert.Contains("Mileage", VehicleSpecValidator.Validate(fresh with { Mileage = 9000 }, VehicleTypeCodes.Car, null, 2026).Keys);
    }

    [Theory]
    [InlineData(VehicleTypeCodes.Motorcycle, 4, 2, "Doors")]        // las motos no tienen puertas
    [InlineData(VehicleTypeCodes.Bus, 2, 5, "Seats")]               // bus: mínimo 10 asientos
    [InlineData(VehicleTypeCodes.Minivan, 4, 3, "Seats")]
    [InlineData(VehicleTypeCodes.Car, 9, 5, "Doors")]
    public void Technical_fields_are_validated_by_type(string type, int doors, int seats, string expectedErrorField)
    {
        var errors = VehicleSpecValidator.Validate(Car() with { Doors = doors, Seats = seats }, type, "ABC123", 2026);
        Assert.Contains(expectedErrorField, errors.Keys);
    }

    [Fact]
    public void Trucks_and_vans_require_load_capacity()
    {
        var truck = Car() with { Doors = 2, Seats = 2 };
        Assert.Contains("LoadCapacityKg", VehicleSpecValidator.Validate(truck, VehicleTypeCodes.Truck, "ABC123", 2026).Keys);
        Assert.Empty(VehicleSpecValidator.Validate(truck with { LoadCapacityKg = 5000 }, VehicleTypeCodes.Truck, "ABC123", 2026));
    }

    [Fact]
    public void Year_rules_and_electric_vehicles()
    {
        Assert.Contains("ManufactureYear", VehicleSpecValidator.Validate(Car() with { ManufactureYear = 1900, ModelYear = 1900 }, VehicleTypeCodes.Car, "ABC123", 2026).Keys);
        Assert.Contains("ModelYear", VehicleSpecValidator.Validate(Car() with { ModelYear = 2018 }, VehicleTypeCodes.Car, "ABC123", 2026).Keys);
        Assert.Contains("EngineDisplacementCc", VehicleSpecValidator.Validate(Car() with { EngineDisplacementCc = null }, VehicleTypeCodes.Car, "ABC123", 2026).Keys);
        Assert.Empty(VehicleSpecValidator.Validate(Car() with { EngineDisplacementCc = null, FuelType = FuelType.Electric }, VehicleTypeCodes.Car, "ABC123", 2026));
    }
}

public class ImageAndMiscRulesTests
{
    [Fact]
    public void Image_format_is_detected_from_content_not_from_name()
    {
        Assert.Equal("image/png", ImageSignature.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0]));
        Assert.Equal("image/jpeg", ImageSignature.Detect([0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.Equal("image/webp", ImageSignature.Detect("RIFF\0\0\0\0WEBP"u8));
        Assert.Null(ImageSignature.Detect("GIF89a"u8));
        Assert.Null(ImageSignature.Detect([0x4D, 0x5A, 0x90, 0x00]));   // ejecutable
        Assert.Null(ImageSignature.Detect("<svg onload=alert(1)>"u8));
        Assert.Null(ImageSignature.Detect([]));
        Assert.Equal(5 * 1024 * 1024, ImageSignature.MaxBytes);
    }

    [Theory]
    [InlineData(0, 0, 0)]       // división entre cero => 0
    [InlineData(5, 0, 0)]
    [InlineData(1, 3, 0.3333)]
    [InlineData(2, 2, 1)]
    public void Rates_are_safe_against_division_by_zero(int n, int d, double expected) =>
        Assert.Equal(expected, DashboardService.SafeRate(n, d));

    [Fact]
    public void Whatsapp_links_are_normalized_and_encoded()
    {
        Assert.Equal("https://wa.me/51987654321?text=Hola%20%26%20gracias", PublicListingService.WhatsAppLink("987 654 321", "Hola & gracias"));
        Assert.StartsWith("https://wa.me/14155550123?", PublicListingService.WhatsAppLink("+1 (415) 555-0123", "x"));
    }
}
