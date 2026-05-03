-- ============================================================
-- EzyGoa TravelManagement - All Pending DB Changes
-- Safe to run: uses IF NOT EXISTS checks throughout
-- ============================================================

-- ── 1. Bookings: make VehicleId nullable ──────────────────────
-- (From migration: External_employee)

-- Drop old FK if it exists (non-nullable version)
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_Bookings_Vehicles_VehicleId'
)
    ALTER TABLE Bookings DROP CONSTRAINT FK_Bookings_Vehicles_VehicleId;
GO

-- Make VehicleId nullable if it isn't already
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Bookings')
      AND name = 'VehicleId'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE Bookings ALTER COLUMN VehicleId INT NULL;
END
GO

-- Re-add FK without cascade delete
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_Bookings_Vehicles_VehicleId'
)
    ALTER TABLE Bookings
        ADD CONSTRAINT FK_Bookings_Vehicles_VehicleId
        FOREIGN KEY (VehicleId) REFERENCES Vehicles(VehicleId);
GO


-- ── 2. ExternalEmployeeCashCollections table ───────────────────
-- (From migration: ExternalEmployeCashCollection_flow)

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ExternalEmployeeCashCollections')
BEGIN
    CREATE TABLE ExternalEmployeeCashCollections (
        Id                  INT             IDENTITY(1,1)   NOT NULL PRIMARY KEY,
        BookingId           INT             NOT NULL,
        ExternalEmployeeId  INT             NULL,
        CashCollectedBy     INT             NOT NULL,
        BookingAmount       DECIMAL(18,2)   NOT NULL,
        CommissionAmount    DECIMAL(18,2)   NOT NULL,
        TotalPaidToVendor   DECIMAL(18,2)   NOT NULL,
        CreatedAt           DATETIME2       NOT NULL,
        SettledAt           DATETIME2       NULL,

        CONSTRAINT FK_ExternalEmployeeCashCollections_Bookings_BookingId
            FOREIGN KEY (BookingId) REFERENCES Bookings(BookingId) ON DELETE CASCADE,

        CONSTRAINT FK_ExternalEmployeeCashCollections_ExternalEmployees_ExternalEmployeeId
            FOREIGN KEY (ExternalEmployeeId) REFERENCES ExternalEmployees(externalEmployeeID)
    );

    CREATE INDEX IX_ExternalEmployeeCashCollections_BookingId
        ON ExternalEmployeeCashCollections (BookingId);

    CREATE INDEX IX_ExternalEmployeeCashCollections_ExternalEmployeeId
        ON ExternalEmployeeCashCollections (ExternalEmployeeId);
END
GO


-- ── 3. Users: new columns ─────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'SalaryDay')
    ALTER TABLE Users ADD SalaryDay INT NOT NULL DEFAULT 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'IsSalaryActive')
    ALTER TABLE Users ADD IsSalaryActive BIT NOT NULL DEFAULT 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'BankAccount')
    ALTER TABLE Users ADD BankAccount NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'IsDeleted')
    ALTER TABLE Users ADD IsDeleted BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'DeletedAt')
    ALTER TABLE Users ADD DeletedAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'DeletedBy')
    ALTER TABLE Users ADD DeletedBy NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'LastAutoSalaryDate')
    ALTER TABLE Users ADD LastAutoSalaryDate DATETIME2 NULL;
GO


-- ── 4. TravelAgents: new columns ─────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TravelAgents') AND name = 'ContactPerson')
    ALTER TABLE TravelAgents ADD ContactPerson NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TravelAgents') AND name = 'WhatsApp')
    ALTER TABLE TravelAgents ADD WhatsApp NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TravelAgents') AND name = 'Address')
    ALTER TABLE TravelAgents ADD Address NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TravelAgents') AND name = 'CommissionPercent')
    ALTER TABLE TravelAgents ADD CommissionPercent DECIMAL(18,2) NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TravelAgents') AND name = 'PaymentTerms')
    ALTER TABLE TravelAgents ADD PaymentTerms NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TravelAgents') AND name = 'BankAccount')
    ALTER TABLE TravelAgents ADD BankAccount NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TravelAgents') AND name = 'IFSC')
    ALTER TABLE TravelAgents ADD IFSC NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TravelAgents') AND name = 'Notes')
    ALTER TABLE TravelAgents ADD Notes NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TravelAgents') AND name = 'IsActive')
    ALTER TABLE TravelAgents ADD IsActive BIT NOT NULL DEFAULT 1;
GO


-- ── 5. Salaries: new columns ──────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('salaries') AND name = 'IsPaid')
    ALTER TABLE salaries ADD IsPaid BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('salaries') AND name = 'PaidDate')
    ALTER TABLE salaries ADD PaidDate DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('salaries') AND name = 'Notes')
    ALTER TABLE salaries ADD Notes NVARCHAR(MAX) NULL;
GO


-- ── 6. Mark EF migration history so dotnet ef doesn't re-apply ──
-- This tells EF that these two pending migrations were already run

IF NOT EXISTS (
    SELECT 1 FROM __EFMigrationsHistory
    WHERE MigrationId = '20260101181333_External_employee'
)
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20260101181333_External_employee', '9.0.2');
GO

IF NOT EXISTS (
    SELECT 1 FROM __EFMigrationsHistory
    WHERE MigrationId = '20260102164507_ExternalEmployeCashCollection_flow'
)
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20260102164507_ExternalEmployeCashCollection_flow', '9.0.2');
GO

-- ── 7. Vehicles: EMI fields (added to model, never migrated) ─
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vehicles') AND name = 'HasEMI')
    ALTER TABLE Vehicles ADD HasEMI BIT NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vehicles') AND name = 'EMIAmount')
    ALTER TABLE Vehicles ADD EMIAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vehicles') AND name = 'EMIDay')
    ALTER TABLE Vehicles ADD EMIDay INT NOT NULL DEFAULT 1;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vehicles') AND name = 'EMIStartDate')
    ALTER TABLE Vehicles ADD EMIStartDate DATE NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vehicles') AND name = 'EMIEndDate')
    ALTER TABLE Vehicles ADD EMIEndDate DATE NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vehicles') AND name = 'EMILender')
    ALTER TABLE Vehicles ADD EMILender NVARCHAR(MAX) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vehicles') AND name = 'TotalEMIs')
    ALTER TABLE Vehicles ADD TotalEMIs INT NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vehicles') AND name = 'PaidEMIs')
    ALTER TABLE Vehicles ADD PaidEMIs INT NOT NULL DEFAULT 0;
GO


-- ── 8. RateCharts table (in DbContext but never migrated) ────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RateCharts')
BEGIN
    CREATE TABLE RateCharts (
        Id               NVARCHAR(450)   NOT NULL PRIMARY KEY,
        TemplateName     NVARCHAR(MAX)   NOT NULL DEFAULT 'Standard Rate Chart',
        AgentName        NVARCHAR(MAX)   NULL,
        AgentNumber      NVARCHAR(MAX)   NULL,
        CompanyName      NVARCHAR(MAX)   NOT NULL DEFAULT 'EZY GOA TRAVELS',
        Tagline          NVARCHAR(MAX)   NULL,
        ValidFrom        DATETIME2       NOT NULL,
        ValidTo          DATETIME2       NOT NULL,
        SpecialDaysNote  NVARCHAR(MAX)   NULL,
        Locations        NVARCHAR(MAX)   NULL,
        VehiclesJson     NVARCHAR(MAX)   NOT NULL DEFAULT '[]',
        RoutesJson       NVARCHAR(MAX)   NOT NULL DEFAULT '[]',
        SurchargesJson   NVARCHAR(MAX)   NOT NULL DEFAULT '[]',
        NotesJson        NVARCHAR(MAX)   NOT NULL DEFAULT '[]',
        FooterJson       NVARCHAR(MAX)   NULL,
        Currency         NVARCHAR(MAX)   NOT NULL DEFAULT 'INR',
        SeasonMode       NVARCHAR(MAX)   NOT NULL DEFAULT 'regular',
        PeakSeasonDates  NVARCHAR(MAX)   NULL,
        CreatedAt        DATETIME2       NOT NULL,
        UpdatedAt        DATETIME2       NOT NULL
    );
END
GO


-- ── 9. Performance indexes ────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bookings_travelDate' AND object_id = OBJECT_ID('Bookings'))
    CREATE INDEX IX_Bookings_travelDate ON Bookings (travelDate);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bookings_VehicleId' AND object_id = OBJECT_ID('Bookings'))
    CREATE INDEX IX_Bookings_VehicleId ON Bookings (VehicleId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bookings_Userid' AND object_id = OBJECT_ID('Bookings'))
    CREATE INDEX IX_Bookings_Userid ON Bookings (Userid);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_vehicleExpences_ExpenseDate' AND object_id = OBJECT_ID('vehicleExpences'))
    CREATE INDEX IX_vehicleExpences_ExpenseDate ON vehicleExpences (ExpenseDate);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_vehicleExpences_VehicleID' AND object_id = OBJECT_ID('vehicleExpences'))
    CREATE INDEX IX_vehicleExpences_VehicleID ON vehicleExpences (VehicleID);
GO


-- ── Done ──────────────────────────────────────────────────────
PRINT 'All pending changes applied successfully.';
