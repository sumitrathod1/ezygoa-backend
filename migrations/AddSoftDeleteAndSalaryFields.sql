-- Migration: Add soft-delete + auto-salary fields to Users
-- Run once against the TravelManagement database

-- Users table
ALTER TABLE Users ADD
    IsDeleted         BIT           NOT NULL DEFAULT 0,
    DeletedAt         DATETIME      NULL,
    DeletedBy         NVARCHAR(256) NULL,
    LastAutoSalaryDate DATETIME     NULL;
GO

-- TravelAgents table (agents redesign fields)
ALTER TABLE TravelAgents ADD
    ContactPerson     NVARCHAR(256) NULL,
    WhatsApp          NVARCHAR(50)  NULL,
    Address           NVARCHAR(500) NULL,
    CommissionPercent DECIMAL(5,2)  NOT NULL DEFAULT 0,
    PaymentTerms      NVARCHAR(256) NULL,
    BankAccount       NVARCHAR(50)  NULL,
    IFSC              NVARCHAR(20)  NULL,
    Notes             NVARCHAR(MAX) NULL,
    IsActive          BIT           NOT NULL DEFAULT 1;
GO

-- Backfill: mark any user with Status=false as soft-deleted
UPDATE Users
SET IsDeleted = 1, DeletedAt = GETDATE()
WHERE Status = 0 AND IsDeleted = 0;
GO
