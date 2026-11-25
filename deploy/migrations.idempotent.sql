IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] uniqueidentifier NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityName] nvarchar(100) NULL,
        [EntityId] nvarchar(64) NULL,
        [Details] nvarchar(max) NULL,
        [IpAddress] nvarchar(45) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [Brands] (
        [Id] int NOT NULL,
        [Name] nvarchar(60) NOT NULL,
        CONSTRAINT [PK_Brands] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [Departments] (
        [Id] int NOT NULL,
        [Code] nchar(2) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [Plans] (
        [Id] int NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(60) NOT NULL,
        [MonthlyPrice] decimal(18,2) NOT NULL,
        [MaxActiveListings] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Plans] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [VehicleTypes] (
        [Id] int NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(60) NOT NULL,
        CONSTRAINT [PK_VehicleTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [Models] (
        [Id] int NOT NULL,
        [BrandId] int NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        CONSTRAINT [PK_Models] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Models_Brands_BrandId] FOREIGN KEY ([BrandId]) REFERENCES [Brands] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [Provinces] (
        [Id] int NOT NULL,
        [DepartmentId] int NOT NULL,
        [Code] nchar(4) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Provinces] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Provinces_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [RoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_RoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoleClaims_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(150) NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [IsRead] bit NOT NULL,
        [ReadAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [FamilyId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(64) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [CreatedByIp] nvarchar(45) NULL,
        [RevokedAt] datetime2 NULL,
        [RevocationReason] nvarchar(30) NULL,
        [ReplacedByTokenHash] nvarchar(64) NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [Subscriptions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [PlanId] int NOT NULL,
        [StartsAt] datetime2 NOT NULL,
        [EndsAt] datetime2 NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Subscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Subscriptions_Plans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [Plans] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Subscriptions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [UserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_UserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [UserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_UserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [UserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_UserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_UserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [Districts] (
        [Id] int NOT NULL,
        [ProvinceId] int NOT NULL,
        [Code] nchar(6) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Districts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Districts_Provinces_ProvinceId] FOREIGN KEY ([ProvinceId]) REFERENCES [Provinces] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [SubscriptionId] uniqueidentifier NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] nchar(3) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Provider] nvarchar(50) NULL,
        [ExternalReference] nvarchar(100) NULL,
        [PaidAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Subscriptions_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [Subscriptions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [BusinessProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [BusinessName] nvarchar(200) NOT NULL,
        [Ruc] nvarchar(11) NULL,
        [Description] nvarchar(1000) NULL,
        [Website] nvarchar(300) NULL,
        [PhoneNumber] nvarchar(20) NULL,
        [DistrictId] int NULL,
        [Address] nvarchar(300) NULL,
        [IsVerified] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_BusinessProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessProfiles_Districts_DistrictId] FOREIGN KEY ([DistrictId]) REFERENCES [Districts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BusinessProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [UserProfiles] (
        [UserId] uniqueidentifier NOT NULL,
        [ProfileType] nvarchar(20) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [PhoneNumber] nvarchar(20) NULL,
        [DocumentNumber] nvarchar(20) NULL,
        [DistrictId] int NULL,
        [Address] nvarchar(300) NULL,
        [AvatarUrl] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_UserProfiles] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_UserProfiles_Districts_DistrictId] FOREIGN KEY ([DistrictId]) REFERENCES [Districts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [Vehicles] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [VehicleTypeId] int NOT NULL,
        [ModelId] int NOT NULL,
        [Year] int NOT NULL,
        [Mileage] int NOT NULL,
        [Condition] nvarchar(20) NOT NULL,
        [FuelType] nvarchar(20) NOT NULL,
        [Transmission] nvarchar(20) NOT NULL,
        [Use] nvarchar(20) NOT NULL,
        [Color] nvarchar(40) NULL,
        [Description] nvarchar(2000) NULL,
        [DistrictId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Vehicles] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Vehicles_Mileage] CHECK ([Mileage] >= 0),
        CONSTRAINT [CK_Vehicles_Year] CHECK ([Year] BETWEEN 1950 AND 2100),
        CONSTRAINT [FK_Vehicles_Districts_DistrictId] FOREIGN KEY ([DistrictId]) REFERENCES [Districts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Vehicles_Models_ModelId] FOREIGN KEY ([ModelId]) REFERENCES [Models] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Vehicles_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Vehicles_VehicleTypes_VehicleTypeId] FOREIGN KEY ([VehicleTypeId]) REFERENCES [VehicleTypes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [Listings] (
        [Id] uniqueidentifier NOT NULL,
        [VehicleId] uniqueidentifier NOT NULL,
        [SellerId] uniqueidentifier NOT NULL,
        [Title] nvarchar(150) NOT NULL,
        [Description] nvarchar(4000) NULL,
        [Status] nvarchar(20) NOT NULL,
        [PublishedAt] datetime2 NULL,
        [ExpiresAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Listings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Listings_Users_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Listings_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [VehicleImages] (
        [Id] uniqueidentifier NOT NULL,
        [VehicleId] uniqueidentifier NOT NULL,
        [Url] nvarchar(500) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsPrimary] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_VehicleImages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VehicleImages_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [ContactEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [ListingId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Channel] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ContactEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContactEvents_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [Favorites] (
        [UserId] uniqueidentifier NOT NULL,
        [ListingId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Favorites] PRIMARY KEY ([UserId], [ListingId]),
        CONSTRAINT [FK_Favorites_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Favorites_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [ListingOperations] (
        [Id] uniqueidentifier NOT NULL,
        [ListingId] uniqueidentifier NOT NULL,
        [OperationType] nvarchar(20) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [Currency] nchar(3) NOT NULL,
        [IsNegotiable] bit NOT NULL,
        [RentPeriod] nvarchar(20) NULL,
        [Deposit] decimal(18,2) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ListingOperations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ListingOperations_Price] CHECK ([Price] >= 0),
        CONSTRAINT [FK_ListingOperations_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [ListingViews] (
        [Id] bigint NOT NULL IDENTITY,
        [ListingId] uniqueidentifier NOT NULL,
        [ViewerId] uniqueidentifier NULL,
        [ViewedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ListingViews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ListingViews_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [Negotiations] (
        [Id] uniqueidentifier NOT NULL,
        [ListingId] uniqueidentifier NOT NULL,
        [ListingOperationId] uniqueidentifier NOT NULL,
        [BuyerId] uniqueidentifier NOT NULL,
        [SellerId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Negotiations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Negotiations_ListingOperations_ListingOperationId] FOREIGN KEY ([ListingOperationId]) REFERENCES [ListingOperations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Negotiations_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Negotiations_Users_BuyerId] FOREIGN KEY ([BuyerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Negotiations_Users_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE TABLE [NegotiationOffers] (
        [Id] uniqueidentifier NOT NULL,
        [NegotiationId] uniqueidentifier NOT NULL,
        [OfferedById] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Message] nvarchar(1000) NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_NegotiationOffers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_NegotiationOffers_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [FK_NegotiationOffers_Negotiations_NegotiationId] FOREIGN KEY ([NegotiationId]) REFERENCES [Negotiations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_NegotiationOffers_Users_OfferedById] FOREIGN KEY ([OfferedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Brands]'))
        SET IDENTITY_INSERT [Brands] ON;
    EXEC(N'INSERT INTO [Brands] ([Id], [Name])
    VALUES (1, N''Toyota''),
    (2, N''Hyundai''),
    (3, N''Kia''),
    (4, N''Nissan''),
    (5, N''Suzuki''),
    (6, N''Chevrolet''),
    (7, N''Honda''),
    (8, N''Mazda''),
    (9, N''Volkswagen''),
    (10, N''Mitsubishi'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Brands]'))
        SET IDENTITY_INSERT [Brands] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'Name') AND [object_id] = OBJECT_ID(N'[Departments]'))
        SET IDENTITY_INSERT [Departments] ON;
    EXEC(N'INSERT INTO [Departments] ([Id], [Code], [Name])
    VALUES (1, N''01'', N''Amazonas''),
    (2, N''02'', N''Áncash''),
    (3, N''03'', N''Apurímac''),
    (4, N''04'', N''Arequipa''),
    (5, N''05'', N''Ayacucho''),
    (6, N''06'', N''Cajamarca''),
    (7, N''07'', N''Callao''),
    (8, N''08'', N''Cusco''),
    (9, N''09'', N''Huancavelica''),
    (10, N''10'', N''Huánuco''),
    (11, N''11'', N''Ica''),
    (12, N''12'', N''Junín''),
    (13, N''13'', N''La Libertad''),
    (14, N''14'', N''Lambayeque''),
    (15, N''15'', N''Lima''),
    (16, N''16'', N''Loreto''),
    (17, N''17'', N''Madre de Dios''),
    (18, N''18'', N''Moquegua''),
    (19, N''19'', N''Pasco''),
    (20, N''20'', N''Piura''),
    (21, N''21'', N''Puno''),
    (22, N''22'', N''San Martín''),
    (23, N''23'', N''Tacna''),
    (24, N''24'', N''Tumbes''),
    (25, N''25'', N''Ucayali'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'Name') AND [object_id] = OBJECT_ID(N'[Departments]'))
        SET IDENTITY_INSERT [Departments] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'IsActive', N'MaxActiveListings', N'MonthlyPrice', N'Name') AND [object_id] = OBJECT_ID(N'[Plans]'))
        SET IDENTITY_INSERT [Plans] ON;
    EXEC(N'INSERT INTO [Plans] ([Id], [Code], [IsActive], [MaxActiveListings], [MonthlyPrice], [Name])
    VALUES (1, N''FREE'', CAST(1 AS bit), 3, 0.0, N''Gratuito''),
    (2, N''PRO'', CAST(1 AS bit), 25, 49.9, N''Profesional''),
    (3, N''BUSINESS'', CAST(1 AS bit), 200, 199.9, N''Empresa'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'IsActive', N'MaxActiveListings', N'MonthlyPrice', N'Name') AND [object_id] = OBJECT_ID(N'[Plans]'))
        SET IDENTITY_INSERT [Plans] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] ON;
    EXEC(N'INSERT INTO [Roles] ([Id], [ConcurrencyStamp], [Name], [NormalizedName])
    VALUES (''11111111-1111-1111-1111-111111111111'', N''seed-admin-role'', N''ADMIN'', N''ADMIN''),
    (''22222222-2222-2222-2222-222222222222'', N''seed-user-role'', N''USER'', N''USER'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'Name') AND [object_id] = OBJECT_ID(N'[VehicleTypes]'))
        SET IDENTITY_INSERT [VehicleTypes] ON;
    EXEC(N'INSERT INTO [VehicleTypes] ([Id], [Code], [Name])
    VALUES (1, N''SEDAN'', N''Sedán''),
    (2, N''HATCHBACK'', N''Hatchback''),
    (3, N''SUV'', N''SUV / Camioneta''),
    (4, N''PICKUP'', N''Pickup''),
    (5, N''VAN'', N''Van / Minivan''),
    (6, N''STATION_WAGON'', N''Station Wagon''),
    (7, N''MOTORCYCLE'', N''Motocicleta''),
    (8, N''MOTOTAXI'', N''Mototaxi''),
    (9, N''TRUCK'', N''Camión'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'Name') AND [object_id] = OBJECT_ID(N'[VehicleTypes]'))
        SET IDENTITY_INSERT [VehicleTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BrandId', N'Name') AND [object_id] = OBJECT_ID(N'[Models]'))
        SET IDENTITY_INSERT [Models] ON;
    EXEC(N'INSERT INTO [Models] ([Id], [BrandId], [Name])
    VALUES (1, 1, N''Yaris''),
    (2, 1, N''Corolla''),
    (3, 1, N''Hilux''),
    (4, 1, N''RAV4''),
    (5, 1, N''Avanza''),
    (6, 2, N''Accent''),
    (7, 2, N''Elantra''),
    (8, 2, N''Tucson''),
    (9, 2, N''Grand i10''),
    (10, 3, N''Rio''),
    (11, 3, N''Picanto''),
    (12, 3, N''Sportage''),
    (13, 3, N''Cerato''),
    (14, 4, N''Versa''),
    (15, 4, N''Sentra''),
    (16, 4, N''Frontier''),
    (17, 4, N''Kicks''),
    (18, 5, N''Swift''),
    (19, 5, N''Dzire''),
    (20, 5, N''Vitara''),
    (21, 5, N''Ertiga''),
    (22, 6, N''Sail''),
    (23, 6, N''Spark''),
    (24, 6, N''N300''),
    (25, 6, N''Onix''),
    (26, 7, N''City''),
    (27, 7, N''Civic''),
    (28, 7, N''CR-V''),
    (29, 8, N''Mazda 2''),
    (30, 8, N''Mazda 3''),
    (31, 8, N''CX-5''),
    (32, 9, N''Gol''),
    (33, 9, N''Polo''),
    (34, 9, N''Tiguan''),
    (35, 10, N''L200''),
    (36, 10, N''ASX'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BrandId', N'Name') AND [object_id] = OBJECT_ID(N'[Models]'))
        SET IDENTITY_INSERT [Models] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DepartmentId', N'Name') AND [object_id] = OBJECT_ID(N'[Provinces]'))
        SET IDENTITY_INSERT [Provinces] ON;
    EXEC(N'INSERT INTO [Provinces] ([Id], [Code], [DepartmentId], [Name])
    VALUES (401, N''0401'', 4, N''Arequipa''),
    (701, N''0701'', 7, N''Callao''),
    (801, N''0801'', 8, N''Cusco''),
    (1301, N''1301'', 13, N''Trujillo''),
    (1401, N''1401'', 14, N''Chiclayo''),
    (1501, N''1501'', 15, N''Lima''),
    (2001, N''2001'', 20, N''Piura'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DepartmentId', N'Name') AND [object_id] = OBJECT_ID(N'[Provinces]'))
        SET IDENTITY_INSERT [Provinces] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'Name', N'ProvinceId') AND [object_id] = OBJECT_ID(N'[Districts]'))
        SET IDENTITY_INSERT [Districts] ON;
    EXEC(N'INSERT INTO [Districts] ([Id], [Code], [Name], [ProvinceId])
    VALUES (40101, N''040101'', N''Arequipa'', 401),
    (70101, N''070101'', N''Callao'', 701),
    (80101, N''080101'', N''Cusco'', 801),
    (130101, N''130101'', N''Trujillo'', 1301),
    (140101, N''140101'', N''Chiclayo'', 1401),
    (150101, N''150101'', N''Lima'', 1501),
    (150103, N''150103'', N''Ate'', 1501),
    (150114, N''150114'', N''La Molina'', 1501),
    (150122, N''150122'', N''Miraflores'', 1501),
    (150130, N''150130'', N''San Borja'', 1501),
    (150131, N''150131'', N''San Isidro'', 1501),
    (150132, N''150132'', N''San Juan de Lurigancho'', 1501),
    (150140, N''150140'', N''Santiago de Surco'', 1501),
    (150141, N''150141'', N''Surquillo'', 1501),
    (200101, N''200101'', N''Piura'', 2001)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'Name', N'ProvinceId') AND [object_id] = OBJECT_ID(N'[Districts]'))
        SET IDENTITY_INSERT [Districts] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CreatedAt] ON [AuditLogs] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId_CreatedAt] ON [AuditLogs] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Brands_Name] ON [Brands] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BusinessProfiles_DistrictId] ON [BusinessProfiles] ([DistrictId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BusinessProfiles_Ruc] ON [BusinessProfiles] ([Ruc]) WHERE [Ruc] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessProfiles_UserId] ON [BusinessProfiles] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ContactEvents_ListingId_CreatedAt] ON [ContactEvents] ([ListingId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Departments_Code] ON [Departments] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Districts_Code] ON [Districts] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Districts_ProvinceId] ON [Districts] ([ProvinceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Favorites_ListingId] ON [Favorites] ([ListingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ListingOperations_ListingId_OperationType] ON [ListingOperations] ([ListingId], [OperationType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ListingOperations_OperationType_Price] ON [ListingOperations] ([OperationType], [Price]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Listings_SellerId] ON [Listings] ([SellerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Listings_Status_PublishedAt] ON [Listings] ([Status], [PublishedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Listings_VehicleId] ON [Listings] ([VehicleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ListingViews_ListingId_ViewedAt] ON [ListingViews] ([ListingId], [ViewedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Models_BrandId_Name] ON [Models] ([BrandId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_NegotiationOffers_NegotiationId] ON [NegotiationOffers] ([NegotiationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_NegotiationOffers_OfferedById] ON [NegotiationOffers] ([OfferedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Negotiations_BuyerId_Status] ON [Negotiations] ([BuyerId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Negotiations_ListingId] ON [Negotiations] ([ListingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Negotiations_ListingOperationId] ON [Negotiations] ([ListingOperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Negotiations_SellerId_Status] ON [Negotiations] ([SellerId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_IsRead] ON [Notifications] ([UserId], [IsRead]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_Provider_ExternalReference] ON [Payments] ([Provider], [ExternalReference]) WHERE [ExternalReference] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_SubscriptionId] ON [Payments] ([SubscriptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_UserId] ON [Payments] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Plans_Code] ON [Plans] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Provinces_Code] ON [Provinces] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Provinces_DepartmentId] ON [Provinces] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_FamilyId] ON [RefreshTokens] ([FamilyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId_ExpiresAt] ON [RefreshTokens] ([UserId], [ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RoleClaims_RoleId] ON [RoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [Roles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Subscriptions_PlanId] ON [Subscriptions] ([PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Subscriptions_UserId_Status] ON [Subscriptions] ([UserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserClaims_UserId] ON [UserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserLogins_UserId] ON [UserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserProfiles_DistrictId] ON [UserProfiles] ([DistrictId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserProfiles_DocumentNumber] ON [UserProfiles] ([DocumentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [EmailIndex] ON [Users] ([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_VehicleImages_VehicleId_SortOrder] ON [VehicleImages] ([VehicleId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vehicles_DistrictId] ON [Vehicles] ([DistrictId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vehicles_ModelId] ON [Vehicles] ([ModelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vehicles_OwnerId] ON [Vehicles] ([OwnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vehicles_VehicleTypeId_Year] ON [Vehicles] ([VehicleTypeId], [Year]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VehicleTypes_Code] ON [VehicleTypes] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125063934_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261125063934_InitialCreate', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] DROP CONSTRAINT [FK_Vehicles_Districts_DistrictId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    DROP INDEX [IX_Vehicles_DistrictId] ON [Vehicles];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    DROP INDEX [IX_Vehicles_VehicleTypeId_Year] ON [Vehicles];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] DROP CONSTRAINT [CK_Vehicles_Mileage];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] DROP CONSTRAINT [CK_Vehicles_Year];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    DROP INDEX [IX_Notifications_UserId_IsRead] ON [Notifications];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    DROP INDEX [IX_Negotiations_ListingOperationId] ON [Negotiations];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    DROP INDEX [IX_NegotiationOffers_NegotiationId] ON [NegotiationOffers];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    DROP INDEX [IX_Listings_SellerId] ON [Listings];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    DROP INDEX [IX_Listings_VehicleId] ON [Listings];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    DROP INDEX [IX_ListingOperations_OperationType_Price] ON [ListingOperations];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [ListingOperations] DROP CONSTRAINT [CK_ListingOperations_Price];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'DELETE FROM [VehicleTypes]
    WHERE [Id] = 6;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Vehicles]') AND [c].[name] = N'Use');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Vehicles] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Vehicles] DROP COLUMN [Use];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[NegotiationOffers]') AND [c].[name] = N'Status');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [NegotiationOffers] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [NegotiationOffers] DROP COLUMN [Status];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ListingOperations]') AND [c].[name] = N'RentPeriod');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [ListingOperations] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [ListingOperations] DROP COLUMN [RentPeriod];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC sp_rename N'[Vehicles].[Year]', N'PreviousOwners', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC sp_rename N'[Vehicles].[DistrictId]', N'Seats', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC sp_rename N'[VehicleImages].[IsPrimary]', N'IsCover', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC sp_rename N'[Listings].[ExpiresAt]', N'ReviewedAt', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC sp_rename N'[ListingOperations].[Price]', N'ReferencePrice', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC sp_rename N'[ListingOperations].[Deposit]', N'WeeklyRate', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [VehicleTypes] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [Doors] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [EngineDisplacementCc] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [HasAirConditioning] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [HasGps] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [HasInsurance] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [HasValidSoat] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [HasValidTechnicalReview] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [LegalOwnerName] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [LegalOwnerRelationship] nvarchar(80) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [LoadCapacityKg] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [ManufactureYear] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [ModelYear] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [OwnershipVerified] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [Plate] nvarchar(12) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [Traction] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [Version] nvarchar(80) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [VehicleImages] ADD [ContentType] nvarchar(30) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [VehicleImages] ADD [ProviderPublicId] nvarchar(255) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [VehicleImages] ADD [SizeBytes] bigint NOT NULL DEFAULT CAST(0 AS bigint);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Subscriptions]') AND [c].[name] = N'EndsAt');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Subscriptions] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [Subscriptions] ALTER COLUMN [EndsAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Subscriptions] ADD [AutoRenew] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Subscriptions] ADD [CancelledAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Subscriptions] ADD [CreatedByAdminId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Subscriptions] ADD [PaymentMode] nvarchar(20) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Provinces] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Plans] ADD [CanExportReports] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Plans] ADD [HasAdvancedStats] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Plans] ADD [HasBusinessProfile] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Plans] ADD [HasPrioritySupport] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Plans] ADD [MaxFeaturedListings] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Plans] ADD [MaxPhotosPerVehicle] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Plans] ADD [SortOrder] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payments]') AND [c].[name] = N'Currency');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Payments] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [Payments] ALTER COLUMN [Currency] nvarchar(3) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Payments] ADD [IsSimulated] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Notifications] ADD [RelatedEntityId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Notifications] ADD [Type] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Negotiations] ADD [Currency] nvarchar(3) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Negotiations] ADD [ExpiresAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Negotiations] ADD [InitialAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Negotiations] ADD [LastOfferSequence] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Negotiations] ADD [Periodicity] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Negotiations] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [NegotiationOffers] ADD [Currency] nvarchar(3) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [NegotiationOffers] ADD [Periodicity] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [NegotiationOffers] ADD [Sequence] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Models] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD [AdditionalConditions] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD [AdvertiserType] nvarchar(20) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD [BusinessName] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD [CommittedOperationId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD [ContactHours] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD [DepartmentId] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD [DistrictId] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD [IsFeatured] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD [LocationReference] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD [PhoneNumber] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD [ProvinceId] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD [RejectionReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD [ReviewedById] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD [WhatsAppNumber] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ListingOperations]') AND [c].[name] = N'Currency');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [ListingOperations] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [ListingOperations] ALTER COLUMN [Currency] nvarchar(3) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [ListingOperations] ADD [AdditionalConditions] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [ListingOperations] ADD [AvailableFrom] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [ListingOperations] ADD [DailyRate] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [ListingOperations] ADD [Guarantee] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [ListingOperations] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [ListingOperations] ADD [MonthlyRate] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [ListingOperations] ADD [SalePrice] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [ListingOperations] ADD [Status] nvarchar(20) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Districts] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Departments] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Brands] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AuditLogs]') AND [c].[name] = N'Details');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [AuditLogs] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [AuditLogs] ALTER COLUMN [Details] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE TABLE [ListingStatusHistory] (
        [Id] bigint NOT NULL IDENTITY,
        [ListingId] uniqueidentifier NOT NULL,
        [FromStatus] nvarchar(20) NULL,
        [ToStatus] nvarchar(20) NOT NULL,
        [OperationType] nvarchar(20) NULL,
        [ChangedById] uniqueidentifier NULL,
        [ChangedAt] datetime2 NOT NULL,
        [Note] nvarchar(500) NULL,
        CONSTRAINT [PK_ListingStatusHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ListingStatusHistory_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE TABLE [ModelVehicleTypes] (
        [ModelId] int NOT NULL,
        [VehicleTypeId] int NOT NULL,
        CONSTRAINT [PK_ModelVehicleTypes] PRIMARY KEY ([ModelId], [VehicleTypeId]),
        CONSTRAINT [FK_ModelVehicleTypes_Models_ModelId] FOREIGN KEY ([ModelId]) REFERENCES [Models] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ModelVehicleTypes_VehicleTypes_VehicleTypeId] FOREIGN KEY ([VehicleTypeId]) REFERENCES [VehicleTypes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE TABLE [OrphanedImages] (
        [Id] bigint NOT NULL IDENTITY,
        [ProviderPublicId] nvarchar(255) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [Attempts] int NOT NULL,
        [LastError] nvarchar(400) NULL,
        CONSTRAINT [PK_OrphanedImages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE TABLE [WorkRentConditions] (
        [ListingOperationId] uniqueidentifier NOT NULL,
        [AllowedUse] nvarchar(20) NOT NULL,
        [Shift] nvarchar(20) NOT NULL,
        [MinExperienceYears] int NOT NULL,
        [LicenseType] nvarchar(30) NULL,
        [WorkZone] nvarchar(200) NULL,
        [MonthlyKmLimit] int NULL,
        [FuelPaidBy] nvarchar(20) NOT NULL,
        [MaintenancePaidBy] nvarchar(20) NOT NULL,
        [InsurancePaidBy] nvarchar(20) NOT NULL,
        [DriverRequirements] nvarchar(1000) NULL,
        CONSTRAINT [PK_WorkRentConditions] PRIMARY KEY ([ListingOperationId]),
        CONSTRAINT [FK_WorkRentConditions_ListingOperations_ListingOperationId] FOREIGN KEY ([ListingOperationId]) REFERENCES [ListingOperations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Brands] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Brands] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Brands] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Brands] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Brands] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 5;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Brands] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 6;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Brands] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 7;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Brands] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 8;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Brands] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 9;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Brands] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 10;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'IsActive', N'Name') AND [object_id] = OBJECT_ID(N'[Brands]'))
        SET IDENTITY_INSERT [Brands] ON;
    EXEC(N'INSERT INTO [Brands] ([Id], [IsActive], [Name])
    VALUES (11, CAST(1 AS bit), N''Bajaj''),
    (12, CAST(1 AS bit), N''Hino''),
    (13, CAST(1 AS bit), N''Yamaha''),
    (14, CAST(1 AS bit), N''Mercedes-Benz'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'IsActive', N'Name') AND [object_id] = OBJECT_ID(N'[Brands]'))
        SET IDENTITY_INSERT [Brands] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 5;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 6;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 7;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 8;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 9;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 10;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 11;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 12;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 13;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 14;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 15;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 16;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 17;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 18;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 19;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 20;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 21;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 22;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 23;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 24;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Departments] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 25;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Districts] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 40101;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Districts] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 70101;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Districts] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 80101;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Districts] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 130101;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Districts] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 140101;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Districts] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 150101;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Districts] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 150103;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Districts] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 150114;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Districts] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 150122;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Districts] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 150130;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Districts] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 150131;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Districts] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 150132;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Districts] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 150140;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Districts] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 150141;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Districts] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 200101;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'ModelId', N'VehicleTypeId') AND [object_id] = OBJECT_ID(N'[ModelVehicleTypes]'))
        SET IDENTITY_INSERT [ModelVehicleTypes] ON;
    EXEC(N'INSERT INTO [ModelVehicleTypes] ([ModelId], [VehicleTypeId])
    VALUES (1, 1),
    (2, 1),
    (3, 4),
    (4, 4),
    (5, 2),
    (6, 1),
    (7, 1),
    (8, 4),
    (9, 1),
    (10, 1),
    (11, 1),
    (12, 4),
    (13, 1),
    (14, 1),
    (15, 1),
    (16, 4),
    (17, 4),
    (18, 1),
    (19, 1),
    (20, 4),
    (21, 2),
    (22, 1),
    (23, 1),
    (24, 5),
    (25, 1),
    (26, 1),
    (27, 1),
    (28, 4),
    (29, 1),
    (30, 1),
    (31, 4),
    (32, 1),
    (33, 1),
    (34, 4),
    (35, 4),
    (36, 4)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'ModelId', N'VehicleTypeId') AND [object_id] = OBJECT_ID(N'[ModelVehicleTypes]'))
        SET IDENTITY_INSERT [ModelVehicleTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 5;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 6;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 7;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 8;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 9;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 10;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 11;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 12;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 13;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 14;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 15;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 16;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 17;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 18;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 19;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 20;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 21;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 22;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 23;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 24;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 25;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 26;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 27;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 28;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 29;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 30;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 31;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 32;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 33;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 34;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 35;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Models] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 36;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BrandId', N'IsActive', N'Name') AND [object_id] = OBJECT_ID(N'[Models]'))
        SET IDENTITY_INSERT [Models] ON;
    EXEC(N'INSERT INTO [Models] ([Id], [BrandId], [IsActive], [Name])
    VALUES (37, 1, CAST(1 AS bit), N''Hiace''),
    (38, 2, CAST(1 AS bit), N''H1''),
    (39, 7, CAST(1 AS bit), N''CB 190R''),
    (45, 2, CAST(1 AS bit), N''County'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BrandId', N'IsActive', N'Name') AND [object_id] = OBJECT_ID(N'[Models]'))
        SET IDENTITY_INSERT [Models] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Plans] SET [CanExportReports] = CAST(0 AS bit), [HasAdvancedStats] = CAST(0 AS bit), [HasBusinessProfile] = CAST(0 AS bit), [HasPrioritySupport] = CAST(0 AS bit), [MaxActiveListings] = 2, [MaxFeaturedListings] = 0, [MaxPhotosPerVehicle] = 5, [Name] = N''Gratis'', [SortOrder] = 1
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Plans] SET [CanExportReports] = CAST(0 AS bit), [Code] = N''BASIC'', [HasAdvancedStats] = CAST(0 AS bit), [HasBusinessProfile] = CAST(0 AS bit), [HasPrioritySupport] = CAST(0 AS bit), [MaxActiveListings] = 5, [MaxFeaturedListings] = 1, [MaxPhotosPerVehicle] = 10, [MonthlyPrice] = 19.0, [Name] = N''Básico'', [SortOrder] = 2
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Plans] SET [CanExportReports] = CAST(0 AS bit), [Code] = N''PROFESSIONAL'', [HasAdvancedStats] = CAST(1 AS bit), [HasBusinessProfile] = CAST(1 AS bit), [HasPrioritySupport] = CAST(0 AS bit), [MaxActiveListings] = 15, [MaxFeaturedListings] = 3, [MaxPhotosPerVehicle] = 15, [MonthlyPrice] = 39.0, [Name] = N''Profesional'', [SortOrder] = 3
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CanExportReports', N'Code', N'HasAdvancedStats', N'HasBusinessProfile', N'HasPrioritySupport', N'IsActive', N'MaxActiveListings', N'MaxFeaturedListings', N'MaxPhotosPerVehicle', N'MonthlyPrice', N'Name', N'SortOrder') AND [object_id] = OBJECT_ID(N'[Plans]'))
        SET IDENTITY_INSERT [Plans] ON;
    EXEC(N'INSERT INTO [Plans] ([Id], [CanExportReports], [Code], [HasAdvancedStats], [HasBusinessProfile], [HasPrioritySupport], [IsActive], [MaxActiveListings], [MaxFeaturedListings], [MaxPhotosPerVehicle], [MonthlyPrice], [Name], [SortOrder])
    VALUES (4, CAST(1 AS bit), N''BUSINESS'', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), 40, 8, 20, 79.0, N''Business'', 4),
    (5, CAST(1 AS bit), N''ENTERPRISE'', CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), 100, 15, 25, 149.0, N''Enterprise'', 5)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CanExportReports', N'Code', N'HasAdvancedStats', N'HasBusinessProfile', N'HasPrioritySupport', N'IsActive', N'MaxActiveListings', N'MaxFeaturedListings', N'MaxPhotosPerVehicle', N'MonthlyPrice', N'Name', N'SortOrder') AND [object_id] = OBJECT_ID(N'[Plans]'))
        SET IDENTITY_INSERT [Plans] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Provinces] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 401;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Provinces] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 701;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Provinces] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 801;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Provinces] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 1301;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Provinces] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 1401;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Provinces] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 1501;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [Provinces] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 2001;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [VehicleTypes] SET [Code] = N''CAR'', [IsActive] = CAST(1 AS bit), [Name] = N''Auto''
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [VehicleTypes] SET [Code] = N''MINIVAN'', [IsActive] = CAST(1 AS bit), [Name] = N''Miniván''
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [VehicleTypes] SET [Code] = N''BUS'', [IsActive] = CAST(1 AS bit), [Name] = N''Bus''
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [VehicleTypes] SET [IsActive] = CAST(1 AS bit), [Name] = N''Camioneta''
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [VehicleTypes] SET [IsActive] = CAST(1 AS bit), [Name] = N''Furgoneta''
    WHERE [Id] = 5;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [VehicleTypes] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 7;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [VehicleTypes] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 8;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'UPDATE [VehicleTypes] SET [IsActive] = CAST(1 AS bit)
    WHERE [Id] = 9;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'ModelId', N'VehicleTypeId') AND [object_id] = OBJECT_ID(N'[ModelVehicleTypes]'))
        SET IDENTITY_INSERT [ModelVehicleTypes] ON;
    EXEC(N'INSERT INTO [ModelVehicleTypes] ([ModelId], [VehicleTypeId])
    VALUES (37, 2),
    (37, 5),
    (38, 2),
    (38, 5),
    (39, 7),
    (45, 3)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'ModelId', N'VehicleTypeId') AND [object_id] = OBJECT_ID(N'[ModelVehicleTypes]'))
        SET IDENTITY_INSERT [ModelVehicleTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BrandId', N'IsActive', N'Name') AND [object_id] = OBJECT_ID(N'[Models]'))
        SET IDENTITY_INSERT [Models] ON;
    EXEC(N'INSERT INTO [Models] ([Id], [BrandId], [IsActive], [Name])
    VALUES (40, 13, CAST(1 AS bit), N''FZ-25''),
    (41, 11, CAST(1 AS bit), N''Torito RE''),
    (42, 12, CAST(1 AS bit), N''Hino 300''),
    (43, 14, CAST(1 AS bit), N''Sprinter''),
    (44, 14, CAST(1 AS bit), N''O 500'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BrandId', N'IsActive', N'Name') AND [object_id] = OBJECT_ID(N'[Models]'))
        SET IDENTITY_INSERT [Models] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'ModelId', N'VehicleTypeId') AND [object_id] = OBJECT_ID(N'[ModelVehicleTypes]'))
        SET IDENTITY_INSERT [ModelVehicleTypes] ON;
    EXEC(N'INSERT INTO [ModelVehicleTypes] ([ModelId], [VehicleTypeId])
    VALUES (40, 7),
    (41, 8),
    (42, 9),
    (43, 2),
    (43, 5),
    (44, 3)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'ModelId', N'VehicleTypeId') AND [object_id] = OBJECT_ID(N'[ModelVehicleTypes]'))
        SET IDENTITY_INSERT [ModelVehicleTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_Vehicles_VehicleTypeId_ModelYear] ON [Vehicles] ([VehicleTypeId], [ModelYear]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Vehicles_Plate_Active] ON [Vehicles] ([Plate]) WHERE [Plate] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'ALTER TABLE [Vehicles] ADD CONSTRAINT [CK_Vehicles_Mileage] CHECK ([Mileage] >= 0 AND [PreviousOwners] >= 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'ALTER TABLE [Vehicles] ADD CONSTRAINT [CK_Vehicles_Years] CHECK ([ManufactureYear] BETWEEN 1950 AND 2100 AND [ModelYear] >= [ManufactureYear])');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_VehicleImages_ProviderPublicId] ON [VehicleImages] ([ProviderPublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_Subscriptions_Status_EndsAt] ON [Subscriptions] ([Status], [EndsAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Subscriptions_ActivePerUser] ON [Subscriptions] ([UserId]) WHERE [Status] = ''Active''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'ALTER TABLE [Plans] ADD CONSTRAINT [CK_Plans_Limits] CHECK ([MaxActiveListings] >= 0 AND [MaxPhotosPerVehicle] >= 0 AND [MaxFeaturedListings] >= 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'ALTER TABLE [Plans] ADD CONSTRAINT [CK_Plans_Price] CHECK ([MonthlyPrice] >= 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_Payments_IsSimulated_Status] ON [Payments] ([IsSimulated], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'ALTER TABLE [Payments] ADD CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] >= 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_IsRead_CreatedAt] ON [Notifications] ([UserId], [IsRead], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_Negotiations_Status_ExpiresAt] ON [Negotiations] ([Status], [ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Negotiations_OpenPerBuyer] ON [Negotiations] ([ListingOperationId], [BuyerId]) WHERE [Status] IN (''Pending'',''CounterOffer'')');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NegotiationOffers_NegotiationId_Sequence] ON [NegotiationOffers] ([NegotiationId], [Sequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_ListingViews_ViewerId_ListingId_ViewedAt] ON [ListingViews] ([ViewerId], [ListingId], [ViewedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_Listings_DepartmentId_ProvinceId_DistrictId] ON [Listings] ([DepartmentId], [ProvinceId], [DistrictId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_Listings_DistrictId] ON [Listings] ([DistrictId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_Listings_ProvinceId] ON [Listings] ([ProvinceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_Listings_SellerId_Status] ON [Listings] ([SellerId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Listings_Vehicle_Active] ON [Listings] ([VehicleId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_ListingOperations_OperationType_Currency_ReferencePrice] ON [ListingOperations] ([OperationType], [Currency], [ReferencePrice]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    EXEC(N'ALTER TABLE [ListingOperations] ADD CONSTRAINT [CK_ListingOperations_Prices] CHECK ([ReferencePrice] >= 0 AND ([SalePrice] IS NULL OR [SalePrice] > 0) AND ([DailyRate] IS NULL OR [DailyRate] > 0) AND ([WeeklyRate] IS NULL OR [WeeklyRate] > 0) AND ([MonthlyRate] IS NULL OR [MonthlyRate] > 0) AND ([Guarantee] IS NULL OR [Guarantee] >= 0))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Action] ON [AuditLogs] ([Action]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_ListingStatusHistory_ListingId_ChangedAt] ON [ListingStatusHistory] ([ListingId], [ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_ListingStatusHistory_ToStatus_ChangedAt] ON [ListingStatusHistory] ([ToStatus], [ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_ModelVehicleTypes_VehicleTypeId] ON [ModelVehicleTypes] ([VehicleTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_OrphanedImages_Attempts_CreatedAt] ON [OrphanedImages] ([Attempts], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    CREATE INDEX [IX_WorkRentConditions_AllowedUse] ON [WorkRentConditions] ([AllowedUse]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD CONSTRAINT [FK_Listings_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD CONSTRAINT [FK_Listings_Districts_DistrictId] FOREIGN KEY ([DistrictId]) REFERENCES [Districts] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    ALTER TABLE [Listings] ADD CONSTRAINT [FK_Listings_Provinces_ProvinceId] FOREIGN KEY ([ProvinceId]) REFERENCES [Provinces] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261125071336_Stage2Marketplace'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261125071336_Stage2Marketplace', N'10.0.15');
END;

COMMIT;
GO

