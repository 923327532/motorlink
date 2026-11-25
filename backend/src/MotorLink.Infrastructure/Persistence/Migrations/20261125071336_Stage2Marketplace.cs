using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MotorLink.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Stage2Marketplace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_Districts_DistrictId",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_DistrictId",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_VehicleTypeId_Year",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_Mileage",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_Year",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Negotiations_ListingOperationId",
                table: "Negotiations");

            migrationBuilder.DropIndex(
                name: "IX_NegotiationOffers_NegotiationId",
                table: "NegotiationOffers");

            migrationBuilder.DropIndex(
                name: "IX_Listings_SellerId",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_VehicleId",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_ListingOperations_OperationType_Price",
                table: "ListingOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ListingOperations_Price",
                table: "ListingOperations");

            migrationBuilder.DeleteData(
                table: "VehicleTypes",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DropColumn(
                name: "Use",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "NegotiationOffers");

            migrationBuilder.DropColumn(
                name: "RentPeriod",
                table: "ListingOperations");

            migrationBuilder.RenameColumn(
                name: "Year",
                table: "Vehicles",
                newName: "PreviousOwners");

            migrationBuilder.RenameColumn(
                name: "DistrictId",
                table: "Vehicles",
                newName: "Seats");

            migrationBuilder.RenameColumn(
                name: "IsPrimary",
                table: "VehicleImages",
                newName: "IsCover");

            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "Listings",
                newName: "ReviewedAt");

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "ListingOperations",
                newName: "ReferencePrice");

            migrationBuilder.RenameColumn(
                name: "Deposit",
                table: "ListingOperations",
                newName: "WeeklyRate");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "VehicleTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Doors",
                table: "Vehicles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EngineDisplacementCc",
                table: "Vehicles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasAirConditioning",
                table: "Vehicles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasGps",
                table: "Vehicles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasInsurance",
                table: "Vehicles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasValidSoat",
                table: "Vehicles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasValidTechnicalReview",
                table: "Vehicles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LegalOwnerName",
                table: "Vehicles",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalOwnerRelationship",
                table: "Vehicles",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoadCapacityKg",
                table: "Vehicles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManufactureYear",
                table: "Vehicles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ModelYear",
                table: "Vehicles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "OwnershipVerified",
                table: "Vehicles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Plate",
                table: "Vehicles",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Traction",
                table: "Vehicles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Version",
                table: "Vehicles",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "VehicleImages",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderPublicId",
                table: "VehicleImages",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "VehicleImages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndsAt",
                table: "Subscriptions",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<bool>(
                name: "AutoRenew",
                table: "Subscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Subscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByAdminId",
                table: "Subscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMode",
                table: "Subscriptions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Provinces",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanExportReports",
                table: "Plans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasAdvancedStats",
                table: "Plans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasBusinessProfile",
                table: "Plans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasPrioritySupport",
                table: "Plans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxFeaturedListings",
                table: "Plans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxPhotosPerVehicle",
                table: "Plans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Plans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Payments",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nchar(3)",
                oldFixedLength: true,
                oldMaxLength: 3);

            migrationBuilder.AddColumn<bool>(
                name: "IsSimulated",
                table: "Payments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedEntityId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Notifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Negotiations",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Negotiations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "InitialAmount",
                table: "Negotiations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "LastOfferSequence",
                table: "Negotiations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Periodicity",
                table: "Negotiations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Negotiations",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "NegotiationOffers",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Periodicity",
                table: "NegotiationOffers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "NegotiationOffers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Models",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AdditionalConditions",
                table: "Listings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdvertiserType",
                table: "Listings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BusinessName",
                table: "Listings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommittedOperationId",
                table: "Listings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactHours",
                table: "Listings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "Listings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DistrictId",
                table: "Listings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "Listings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LocationReference",
                table: "Listings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Listings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProvinceId",
                table: "Listings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Listings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedById",
                table: "Listings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Listings",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppNumber",
                table: "Listings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "ListingOperations",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nchar(3)",
                oldFixedLength: true,
                oldMaxLength: 3);

            migrationBuilder.AddColumn<string>(
                name: "AdditionalConditions",
                table: "ListingOperations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailableFrom",
                table: "ListingOperations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyRate",
                table: "ListingOperations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Guarantee",
                table: "ListingOperations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ListingOperations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyRate",
                table: "ListingOperations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalePrice",
                table: "ListingOperations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ListingOperations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Districts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Departments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Brands",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Details",
                table: "AuditLogs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ListingStatusHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ChangedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingStatusHistory_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelVehicleTypes",
                columns: table => new
                {
                    ModelId = table.Column<int>(type: "int", nullable: false),
                    VehicleTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelVehicleTypes", x => new { x.ModelId, x.VehicleTypeId });
                    table.ForeignKey(
                        name: "FK_ModelVehicleTypes_Models_ModelId",
                        column: x => x.ModelId,
                        principalTable: "Models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModelVehicleTypes_VehicleTypes_VehicleTypeId",
                        column: x => x.VehicleTypeId,
                        principalTable: "VehicleTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrphanedImages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProviderPublicId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrphanedImages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkRentConditions",
                columns: table => new
                {
                    ListingOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowedUse = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Shift = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MinExperienceYears = table.Column<int>(type: "int", nullable: false),
                    LicenseType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    WorkZone = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MonthlyKmLimit = table.Column<int>(type: "int", nullable: true),
                    FuelPaidBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MaintenancePaidBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InsurancePaidBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DriverRequirements = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkRentConditions", x => x.ListingOperationId);
                    table.ForeignKey(
                        name: "FK_WorkRentConditions_ListingOperations_ListingOperationId",
                        column: x => x.ListingOperationId,
                        principalTable: "ListingOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: 3,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: 4,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: 5,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: 6,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: 7,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: 8,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: 9,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: 10,
                column: "IsActive",
                value: true);

            migrationBuilder.InsertData(
                table: "Brands",
                columns: new[] { "Id", "IsActive", "Name" },
                values: new object[,]
                {
                    { 11, true, "Bajaj" },
                    { 12, true, "Hino" },
                    { 13, true, "Yamaha" },
                    { 14, true, "Mercedes-Benz" }
                });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 3,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 4,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 5,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 6,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 7,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 9,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 10,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 11,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 12,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 13,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 14,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 15,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 16,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 17,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 18,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 19,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 20,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 21,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 22,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 23,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 24,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 25,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Districts",
                keyColumn: "Id",
                keyValue: 40101,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Districts",
                keyColumn: "Id",
                keyValue: 70101,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Districts",
                keyColumn: "Id",
                keyValue: 80101,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Districts",
                keyColumn: "Id",
                keyValue: 130101,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Districts",
                keyColumn: "Id",
                keyValue: 140101,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Districts",
                keyColumn: "Id",
                keyValue: 150101,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Districts",
                keyColumn: "Id",
                keyValue: 150103,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Districts",
                keyColumn: "Id",
                keyValue: 150114,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Districts",
                keyColumn: "Id",
                keyValue: 150122,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Districts",
                keyColumn: "Id",
                keyValue: 150130,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Districts",
                keyColumn: "Id",
                keyValue: 150131,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Districts",
                keyColumn: "Id",
                keyValue: 150132,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Districts",
                keyColumn: "Id",
                keyValue: 150140,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Districts",
                keyColumn: "Id",
                keyValue: 150141,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Districts",
                keyColumn: "Id",
                keyValue: 200101,
                column: "IsActive",
                value: true);

            migrationBuilder.InsertData(
                table: "ModelVehicleTypes",
                columns: new[] { "ModelId", "VehicleTypeId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 3, 4 },
                    { 4, 4 },
                    { 5, 2 },
                    { 6, 1 },
                    { 7, 1 },
                    { 8, 4 },
                    { 9, 1 },
                    { 10, 1 },
                    { 11, 1 },
                    { 12, 4 },
                    { 13, 1 },
                    { 14, 1 },
                    { 15, 1 },
                    { 16, 4 },
                    { 17, 4 },
                    { 18, 1 },
                    { 19, 1 },
                    { 20, 4 },
                    { 21, 2 },
                    { 22, 1 },
                    { 23, 1 },
                    { 24, 5 },
                    { 25, 1 },
                    { 26, 1 },
                    { 27, 1 },
                    { 28, 4 },
                    { 29, 1 },
                    { 30, 1 },
                    { 31, 4 },
                    { 32, 1 },
                    { 33, 1 },
                    { 34, 4 },
                    { 35, 4 },
                    { 36, 4 }
                });

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 3,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 4,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 5,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 6,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 7,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 8,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 9,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 10,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 11,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 12,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 13,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 14,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 15,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 16,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 17,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 18,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 19,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 20,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 21,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 22,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 23,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 24,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 25,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 26,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 27,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 28,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 29,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 30,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 31,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 32,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 33,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 34,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 35,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 36,
                column: "IsActive",
                value: true);

            migrationBuilder.InsertData(
                table: "Models",
                columns: new[] { "Id", "BrandId", "IsActive", "Name" },
                values: new object[,]
                {
                    { 37, 1, true, "Hiace" },
                    { 38, 2, true, "H1" },
                    { 39, 7, true, "CB 190R" },
                    { 45, 2, true, "County" }
                });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CanExportReports", "HasAdvancedStats", "HasBusinessProfile", "HasPrioritySupport", "MaxActiveListings", "MaxFeaturedListings", "MaxPhotosPerVehicle", "Name", "SortOrder" },
                values: new object[] { false, false, false, false, 2, 0, 5, "Gratis", 1 });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CanExportReports", "Code", "HasAdvancedStats", "HasBusinessProfile", "HasPrioritySupport", "MaxActiveListings", "MaxFeaturedListings", "MaxPhotosPerVehicle", "MonthlyPrice", "Name", "SortOrder" },
                values: new object[] { false, "BASIC", false, false, false, 5, 1, 10, 19m, "Básico", 2 });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CanExportReports", "Code", "HasAdvancedStats", "HasBusinessProfile", "HasPrioritySupport", "MaxActiveListings", "MaxFeaturedListings", "MaxPhotosPerVehicle", "MonthlyPrice", "Name", "SortOrder" },
                values: new object[] { false, "PROFESSIONAL", true, true, false, 15, 3, 15, 39m, "Profesional", 3 });

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "Id", "CanExportReports", "Code", "HasAdvancedStats", "HasBusinessProfile", "HasPrioritySupport", "IsActive", "MaxActiveListings", "MaxFeaturedListings", "MaxPhotosPerVehicle", "MonthlyPrice", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 4, true, "BUSINESS", true, true, false, true, 40, 8, 20, 79m, "Business", 4 },
                    { 5, true, "ENTERPRISE", true, true, true, true, 100, 15, 25, 149m, "Enterprise", 5 }
                });

            migrationBuilder.UpdateData(
                table: "Provinces",
                keyColumn: "Id",
                keyValue: 401,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Provinces",
                keyColumn: "Id",
                keyValue: 701,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Provinces",
                keyColumn: "Id",
                keyValue: 801,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Provinces",
                keyColumn: "Id",
                keyValue: 1301,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Provinces",
                keyColumn: "Id",
                keyValue: 1401,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Provinces",
                keyColumn: "Id",
                keyValue: 1501,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Provinces",
                keyColumn: "Id",
                keyValue: 2001,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "VehicleTypes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Code", "IsActive", "Name" },
                values: new object[] { "CAR", true, "Auto" });

            migrationBuilder.UpdateData(
                table: "VehicleTypes",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Code", "IsActive", "Name" },
                values: new object[] { "MINIVAN", true, "Miniván" });

            migrationBuilder.UpdateData(
                table: "VehicleTypes",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Code", "IsActive", "Name" },
                values: new object[] { "BUS", true, "Bus" });

            migrationBuilder.UpdateData(
                table: "VehicleTypes",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "IsActive", "Name" },
                values: new object[] { true, "Camioneta" });

            migrationBuilder.UpdateData(
                table: "VehicleTypes",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "IsActive", "Name" },
                values: new object[] { true, "Furgoneta" });

            migrationBuilder.UpdateData(
                table: "VehicleTypes",
                keyColumn: "Id",
                keyValue: 7,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "VehicleTypes",
                keyColumn: "Id",
                keyValue: 8,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "VehicleTypes",
                keyColumn: "Id",
                keyValue: 9,
                column: "IsActive",
                value: true);

            migrationBuilder.InsertData(
                table: "ModelVehicleTypes",
                columns: new[] { "ModelId", "VehicleTypeId" },
                values: new object[,]
                {
                    { 37, 2 },
                    { 37, 5 },
                    { 38, 2 },
                    { 38, 5 },
                    { 39, 7 },
                    { 45, 3 }
                });

            migrationBuilder.InsertData(
                table: "Models",
                columns: new[] { "Id", "BrandId", "IsActive", "Name" },
                values: new object[,]
                {
                    { 40, 13, true, "FZ-25" },
                    { 41, 11, true, "Torito RE" },
                    { 42, 12, true, "Hino 300" },
                    { 43, 14, true, "Sprinter" },
                    { 44, 14, true, "O 500" }
                });

            migrationBuilder.InsertData(
                table: "ModelVehicleTypes",
                columns: new[] { "ModelId", "VehicleTypeId" },
                values: new object[,]
                {
                    { 40, 7 },
                    { 41, 8 },
                    { 42, 9 },
                    { 43, 2 },
                    { 43, 5 },
                    { 44, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_VehicleTypeId_ModelYear",
                table: "Vehicles",
                columns: new[] { "VehicleTypeId", "ModelYear" });

            migrationBuilder.CreateIndex(
                name: "UX_Vehicles_Plate_Active",
                table: "Vehicles",
                column: "Plate",
                unique: true,
                filter: "[Plate] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_Mileage",
                table: "Vehicles",
                sql: "[Mileage] >= 0 AND [PreviousOwners] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_Years",
                table: "Vehicles",
                sql: "[ManufactureYear] BETWEEN 1950 AND 2100 AND [ModelYear] >= [ManufactureYear]");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleImages_ProviderPublicId",
                table: "VehicleImages",
                column: "ProviderPublicId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Status_EndsAt",
                table: "Subscriptions",
                columns: new[] { "Status", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "UX_Subscriptions_ActivePerUser",
                table: "Subscriptions",
                column: "UserId",
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Plans_Limits",
                table: "Plans",
                sql: "[MaxActiveListings] >= 0 AND [MaxPhotosPerVehicle] >= 0 AND [MaxFeaturedListings] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Plans_Price",
                table: "Plans",
                sql: "[MonthlyPrice] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_IsSimulated_Status",
                table: "Payments",
                columns: new[] { "IsSimulated", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Amount",
                table: "Payments",
                sql: "[Amount] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Negotiations_Status_ExpiresAt",
                table: "Negotiations",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "UX_Negotiations_OpenPerBuyer",
                table: "Negotiations",
                columns: new[] { "ListingOperationId", "BuyerId" },
                unique: true,
                filter: "[Status] IN ('Pending','CounterOffer')");

            migrationBuilder.CreateIndex(
                name: "IX_NegotiationOffers_NegotiationId_Sequence",
                table: "NegotiationOffers",
                columns: new[] { "NegotiationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingViews_ViewerId_ListingId_ViewedAt",
                table: "ListingViews",
                columns: new[] { "ViewerId", "ListingId", "ViewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_DepartmentId_ProvinceId_DistrictId",
                table: "Listings",
                columns: new[] { "DepartmentId", "ProvinceId", "DistrictId" });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_DistrictId",
                table: "Listings",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_ProvinceId",
                table: "Listings",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_SellerId_Status",
                table: "Listings",
                columns: new[] { "SellerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_Listings_Vehicle_Active",
                table: "Listings",
                column: "VehicleId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ListingOperations_OperationType_Currency_ReferencePrice",
                table: "ListingOperations",
                columns: new[] { "OperationType", "Currency", "ReferencePrice" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ListingOperations_Prices",
                table: "ListingOperations",
                sql: "[ReferencePrice] >= 0 AND ([SalePrice] IS NULL OR [SalePrice] > 0) AND ([DailyRate] IS NULL OR [DailyRate] > 0) AND ([WeeklyRate] IS NULL OR [WeeklyRate] > 0) AND ([MonthlyRate] IS NULL OR [MonthlyRate] > 0) AND ([Guarantee] IS NULL OR [Guarantee] >= 0)");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_ListingStatusHistory_ListingId_ChangedAt",
                table: "ListingStatusHistory",
                columns: new[] { "ListingId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ListingStatusHistory_ToStatus_ChangedAt",
                table: "ListingStatusHistory",
                columns: new[] { "ToStatus", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelVehicleTypes_VehicleTypeId",
                table: "ModelVehicleTypes",
                column: "VehicleTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrphanedImages_Attempts_CreatedAt",
                table: "OrphanedImages",
                columns: new[] { "Attempts", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkRentConditions_AllowedUse",
                table: "WorkRentConditions",
                column: "AllowedUse");

            migrationBuilder.AddForeignKey(
                name: "FK_Listings_Departments_DepartmentId",
                table: "Listings",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Listings_Districts_DistrictId",
                table: "Listings",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Listings_Provinces_ProvinceId",
                table: "Listings",
                column: "ProvinceId",
                principalTable: "Provinces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Listings_Departments_DepartmentId",
                table: "Listings");

            migrationBuilder.DropForeignKey(
                name: "FK_Listings_Districts_DistrictId",
                table: "Listings");

            migrationBuilder.DropForeignKey(
                name: "FK_Listings_Provinces_ProvinceId",
                table: "Listings");

            migrationBuilder.DropTable(
                name: "ListingStatusHistory");

            migrationBuilder.DropTable(
                name: "ModelVehicleTypes");

            migrationBuilder.DropTable(
                name: "OrphanedImages");

            migrationBuilder.DropTable(
                name: "WorkRentConditions");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_VehicleTypeId_ModelYear",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "UX_Vehicles_Plate_Active",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_Mileage",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_Years",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_VehicleImages_ProviderPublicId",
                table: "VehicleImages");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_Status_EndsAt",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "UX_Subscriptions_ActivePerUser",
                table: "Subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Plans_Limits",
                table: "Plans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Plans_Price",
                table: "Plans");

            migrationBuilder.DropIndex(
                name: "IX_Payments_IsSimulated_Status",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Amount",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Negotiations_Status_ExpiresAt",
                table: "Negotiations");

            migrationBuilder.DropIndex(
                name: "UX_Negotiations_OpenPerBuyer",
                table: "Negotiations");

            migrationBuilder.DropIndex(
                name: "IX_NegotiationOffers_NegotiationId_Sequence",
                table: "NegotiationOffers");

            migrationBuilder.DropIndex(
                name: "IX_ListingViews_ViewerId_ListingId_ViewedAt",
                table: "ListingViews");

            migrationBuilder.DropIndex(
                name: "IX_Listings_DepartmentId_ProvinceId_DistrictId",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_DistrictId",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_ProvinceId",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_SellerId_Status",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "UX_Listings_Vehicle_Active",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_ListingOperations_OperationType_Currency_ReferencePrice",
                table: "ListingOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ListingOperations_Prices",
                table: "ListingOperations");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.DeleteData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "Models",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Brands",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "VehicleTypes");

            migrationBuilder.DropColumn(
                name: "Doors",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "EngineDisplacementCc",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "HasAirConditioning",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "HasGps",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "HasInsurance",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "HasValidSoat",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "HasValidTechnicalReview",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "LegalOwnerName",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "LegalOwnerRelationship",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "LoadCapacityKg",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ManufactureYear",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ModelYear",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "OwnershipVerified",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Plate",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Traction",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "VehicleImages");

            migrationBuilder.DropColumn(
                name: "ProviderPublicId",
                table: "VehicleImages");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "VehicleImages");

            migrationBuilder.DropColumn(
                name: "AutoRenew",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CreatedByAdminId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PaymentMode",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Provinces");

            migrationBuilder.DropColumn(
                name: "CanExportReports",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "HasAdvancedStats",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "HasBusinessProfile",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "HasPrioritySupport",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "MaxFeaturedListings",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "MaxPhotosPerVehicle",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "IsSimulated",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RelatedEntityId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Negotiations");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Negotiations");

            migrationBuilder.DropColumn(
                name: "InitialAmount",
                table: "Negotiations");

            migrationBuilder.DropColumn(
                name: "LastOfferSequence",
                table: "Negotiations");

            migrationBuilder.DropColumn(
                name: "Periodicity",
                table: "Negotiations");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Negotiations");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "NegotiationOffers");

            migrationBuilder.DropColumn(
                name: "Periodicity",
                table: "NegotiationOffers");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "NegotiationOffers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "AdditionalConditions",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "AdvertiserType",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "BusinessName",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "CommittedOperationId",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "ContactHours",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "LocationReference",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "ProvinceId",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "ReviewedById",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "WhatsAppNumber",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "AdditionalConditions",
                table: "ListingOperations");

            migrationBuilder.DropColumn(
                name: "AvailableFrom",
                table: "ListingOperations");

            migrationBuilder.DropColumn(
                name: "DailyRate",
                table: "ListingOperations");

            migrationBuilder.DropColumn(
                name: "Guarantee",
                table: "ListingOperations");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ListingOperations");

            migrationBuilder.DropColumn(
                name: "MonthlyRate",
                table: "ListingOperations");

            migrationBuilder.DropColumn(
                name: "SalePrice",
                table: "ListingOperations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ListingOperations");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Brands");

            migrationBuilder.RenameColumn(
                name: "Seats",
                table: "Vehicles",
                newName: "DistrictId");

            migrationBuilder.RenameColumn(
                name: "PreviousOwners",
                table: "Vehicles",
                newName: "Year");

            migrationBuilder.RenameColumn(
                name: "IsCover",
                table: "VehicleImages",
                newName: "IsPrimary");

            migrationBuilder.RenameColumn(
                name: "ReviewedAt",
                table: "Listings",
                newName: "ExpiresAt");

            migrationBuilder.RenameColumn(
                name: "WeeklyRate",
                table: "ListingOperations",
                newName: "Deposit");

            migrationBuilder.RenameColumn(
                name: "ReferencePrice",
                table: "ListingOperations",
                newName: "Price");

            migrationBuilder.AddColumn<string>(
                name: "Use",
                table: "Vehicles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndsAt",
                table: "Subscriptions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Payments",
                type: "nchar(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "NegotiationOffers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "ListingOperations",
                type: "nchar(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AddColumn<string>(
                name: "RentPeriod",
                table: "ListingOperations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Details",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "MaxActiveListings", "Name" },
                values: new object[] { 3, "Gratuito" });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Code", "MaxActiveListings", "MonthlyPrice", "Name" },
                values: new object[] { "PRO", 25, 49.90m, "Profesional" });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Code", "MaxActiveListings", "MonthlyPrice", "Name" },
                values: new object[] { "BUSINESS", 200, 199.90m, "Empresa" });

            migrationBuilder.UpdateData(
                table: "VehicleTypes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Code", "Name" },
                values: new object[] { "SEDAN", "Sedán" });

            migrationBuilder.UpdateData(
                table: "VehicleTypes",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Code", "Name" },
                values: new object[] { "HATCHBACK", "Hatchback" });

            migrationBuilder.UpdateData(
                table: "VehicleTypes",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Code", "Name" },
                values: new object[] { "SUV", "SUV / Camioneta" });

            migrationBuilder.UpdateData(
                table: "VehicleTypes",
                keyColumn: "Id",
                keyValue: 4,
                column: "Name",
                value: "Pickup");

            migrationBuilder.UpdateData(
                table: "VehicleTypes",
                keyColumn: "Id",
                keyValue: 5,
                column: "Name",
                value: "Van / Minivan");

            migrationBuilder.InsertData(
                table: "VehicleTypes",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[] { 6, "STATION_WAGON", "Station Wagon" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_DistrictId",
                table: "Vehicles",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_VehicleTypeId_Year",
                table: "Vehicles",
                columns: new[] { "VehicleTypeId", "Year" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_Mileage",
                table: "Vehicles",
                sql: "[Mileage] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_Year",
                table: "Vehicles",
                sql: "[Year] BETWEEN 1950 AND 2100");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_Negotiations_ListingOperationId",
                table: "Negotiations",
                column: "ListingOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_NegotiationOffers_NegotiationId",
                table: "NegotiationOffers",
                column: "NegotiationId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_SellerId",
                table: "Listings",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_VehicleId",
                table: "Listings",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingOperations_OperationType_Price",
                table: "ListingOperations",
                columns: new[] { "OperationType", "Price" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ListingOperations_Price",
                table: "ListingOperations",
                sql: "[Price] >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_Districts_DistrictId",
                table: "Vehicles",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
