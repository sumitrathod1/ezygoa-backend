-- =====================================================================
--  EzyGoa — Self-drive rental catalog
--  Source: https://zipgoa.com/self-drive  (embedded fleet JSON)
--  Verified from raw page HTML on 2026-09-12.
--  Target: PostgreSQL (Npgsql)   OrgId = 1
--
--  Adds 19 PLACEHOLDER vehicles (not real cars you own) so that
--  RentalRates has something to point its VehicleId at, then attaches
--  the published rate to each one.
--
--  They are identified by "VehicleNumber" LIKE 'RENTAL-%'.
--  ------------------------------------------------------------------
--  !! READ THIS !!  Nothing filters them out yet. Until you add the
--  filters in the "WHAT TO PATCH" section at the bottom, these 19 cars
--  WILL appear in: the dashboard vehicle count, the booking form's
--  vehicle dropdown, the vehicle list page, and the expense/document
--  forms. That is expected with this approach, not a bug.
--  ------------------------------------------------------------------
--
--  Site-wide rental terms (apply to every car):
--    "Unlimited km within Goa"  -> KmPerDay = NULL, ExtraKmRate = NULL
--    "Minimum booking: 2 days"  -> MinDays = 2
--    No weekly rate published   -> WeeklyRate = NULL
--    Late return Rs.200/hour · home delivery North Goa Rs.200
--    Rental day = 9 AM to next day 9 AM · driver age 21+
-- =====================================================================

BEGIN;

-- ---------------------------------------------------------------------
-- PART 1 — Placeholder vehicles
--
-- VehicleType enum: 0=HatchBack 1=Sedan 2=Suv 3=TT17Seater 4=TT20Seater
--                   5=Bus30 6=Bus40 7=Bus60 8=Notspecified
-- RegistrationDate is NOT NULL, so a deliberately fake sentinel
-- (2026-01-01) is used to make these obviously not real assets.
-- ---------------------------------------------------------------------
INSERT INTO "Vehicles"
    ("VehicleName", "VehicleNumber", "VehicleType", "RegistrationDate",
     "VehicleAge", "Seatingcapacity", "HasEMI", "EMIAmount", "EMIDay",
     "EMIStartDate", "EMIEndDate", "EMILender", "TotalEMIs", "PaidEMIs", "OrgId")
SELECT c.name, c.number, c.vtype, DATE '2026-01-01',
       0, c.seats, FALSE, 0, 1, NULL, NULL, NULL, 0, 0, 1
FROM (VALUES
    --  name                                             number               type seats
    ('Maruti Swift New (Manual)',                     'RENTAL-SWIFT-MT',      0,  5),
    ('Maruti Swift New (Automatic)',                  'RENTAL-SWIFT-AT',      0,  5),
    ('Maruti Baleno New (Manual)',                    'RENTAL-BALENO-MT',     0,  5),
    ('Maruti Baleno New (Automatic)',                 'RENTAL-BALENO-AT',     0,  5),
    ('Hyundai i20 New (Manual)',                      'RENTAL-I20-MT',        0,  5),
    ('Hyundai i20 New (Automatic)',                   'RENTAL-I20-AT',        0,  5),
    ('Swift Dzire New (Manual)',                      'RENTAL-DZIRE-MT',      1,  5),
    ('Swift Dzire New (Automatic)',                   'RENTAL-DZIRE-AT',      1,  5),
    ('Maruti Ertiga New 7-Seater (Manual)',           'RENTAL-ERTIGA-MT',     2,  7),
    ('Maruti Ertiga New 7-Seater (Automatic)',        'RENTAL-ERTIGA-AT',     2,  7),
    ('Hyundai Creta New (Manual)',                    'RENTAL-CRETA-MT',      2,  5),
    ('Hyundai Creta New (Automatic) Panoramic Sunroof','RENTAL-CRETA-AT',     2,  5),
    ('Toyota Innova Crysta (Manual)',                 'RENTAL-INNOVA-MT',     2,  7),
    ('Toyota Innova Crysta (Automatic)',              'RENTAL-INNOVA-AT',     2,  7),
    ('Toyota Innova Hycross Hybrid (Automatic)',      'RENTAL-HYCROSS-AT',    2,  7),
    ('Mahindra Thar New Hard Top (Manual)',           'RENTAL-THAR-HT-MT',    2,  4),
    ('Mahindra Thar New Hard Top (Automatic)',        'RENTAL-THAR-HT-AT',    2,  4),
    ('Mahindra Thar Convertible 4x4 Soft Top (Manual)',   'RENTAL-THAR-CV-MT', 2, 4),
    ('Mahindra Thar Convertible 4x4 Soft Top (Automatic)','RENTAL-THAR-CV-AT', 2, 4)
) AS c(name, number, vtype, seats)
WHERE NOT EXISTS (
    SELECT 1 FROM "Vehicles" v
    WHERE v."VehicleNumber" = c.number AND v."OrgId" = 1
);


-- ---------------------------------------------------------------------
-- PART 2 — Rental rate for each placeholder
-- Joined on VehicleNumber, so the mapping is exact (no name guessing).
-- ---------------------------------------------------------------------
INSERT INTO "RentalRates"
    ("VehicleId", "PricePerDay", "WeeklyRate", "SecurityDeposit",
     "MinDays", "KmPerDay", "ExtraKmRate", "IsActive", "OrgId")
SELECT v."VehicleId",
       rc.price,
       NULL,       -- WeeklyRate   — not published
       rc.deposit,
       2,          -- MinDays      — "Minimum booking: 2 days"
       NULL,       -- KmPerDay     — "Unlimited km within Goa"
       NULL,       -- ExtraKmRate  — unlimited, so none
       TRUE,
       1
FROM (VALUES
    --  number                price deposit
    ('RENTAL-SWIFT-MT',       1400,  3000),
    ('RENTAL-SWIFT-AT',       1600,  3500),
    ('RENTAL-BALENO-MT',      1500,  3000),
    ('RENTAL-BALENO-AT',      1800,  4000),
    ('RENTAL-I20-MT',         1500,  3500),
    ('RENTAL-I20-AT',         1800,  4000),
    ('RENTAL-DZIRE-MT',       1500,  3500),
    ('RENTAL-DZIRE-AT',       1800,  4000),
    ('RENTAL-ERTIGA-MT',      2200,  5000),
    ('RENTAL-ERTIGA-AT',      2800,  6000),
    ('RENTAL-CRETA-MT',       2500,  5000),
    ('RENTAL-CRETA-AT',       3500,  6000),
    ('RENTAL-INNOVA-MT',      3000,  6000),
    ('RENTAL-INNOVA-AT',      3500,  7000),
    ('RENTAL-HYCROSS-AT',     4000,  8000),
    ('RENTAL-THAR-HT-MT',     3500,  7000),
    ('RENTAL-THAR-HT-AT',     3500,  7000),
    ('RENTAL-THAR-CV-MT',     3500,  7000),
    ('RENTAL-THAR-CV-AT',     4000,  8000)
) AS rc(number, price, deposit)
JOIN "Vehicles" v ON v."VehicleNumber" = rc.number AND v."OrgId" = 1
WHERE NOT EXISTS (
    SELECT 1 FROM "RentalRates" rr
    WHERE rr."VehicleId" = v."VehicleId" AND rr."OrgId" = 1
);

COMMIT;


-- =====================================================================
--  VERIFY
-- =====================================================================

-- The catalog — expect 19 rows, cheapest first
-- SELECT v."VehicleId", v."VehicleName", v."VehicleNumber",
--        rr."PricePerDay", rr."SecurityDeposit", rr."MinDays"
-- FROM "Vehicles" v
-- JOIN "RentalRates" rr ON rr."VehicleId" = v."VehicleId"
-- WHERE v."VehicleNumber" LIKE 'RENTAL-%' AND v."OrgId" = 1
-- ORDER BY rr."PricePerDay", v."VehicleName";

-- Your REAL vehicles are untouched — expect your original 6
-- SELECT "VehicleId", "VehicleName", "VehicleNumber"
-- FROM "Vehicles"
-- WHERE "OrgId" = 1 AND COALESCE("VehicleNumber",'') NOT LIKE 'RENTAL-%'
-- ORDER BY "VehicleId";

-- Any placeholder that duplicates a car you actually own?
-- (You already have a real "Baleno automatic" with a rate — that row
--  stays as-is; the RENTAL-BALENO-AT placeholder is the price-list entry.)
-- SELECT r."VehicleName" AS catalog, o."VehicleName" AS you_own
-- FROM "Vehicles" r
-- JOIN "Vehicles" o
--   ON  o."OrgId" = 1
--   AND COALESCE(o."VehicleNumber",'') NOT LIKE 'RENTAL-%'
--   AND (   r."VehicleName" ILIKE '%' || split_part(o."VehicleName", ' ', 1) || '%'
--        OR o."VehicleName" ILIKE '%' || split_part(r."VehicleName", ' ', 2) || '%')
-- WHERE r."VehicleNumber" LIKE 'RENTAL-%' AND r."OrgId" = 1;


-- =====================================================================
--  WHAT TO PATCH  — add this filter to keep the catalog out of
--  taxi-side screens.  Filter expression:
--
--      COALESCE(v."VehicleNumber", '') NOT LIKE 'RENTAL-%'
--  EF Core equivalent:
--      .Where(v => v.VehicleNumber == null
--               || !v.VehicleNumber.StartsWith("RENTAL-"))
--
--  Apply in:
--    TravelManagement_API/Controllers/VehicleController.cs:241
--        GetAvailableVehicles  -> booking form's vehicle dropdown
--    TravelManagement_API/Controllers/DashboardController.cs:96
--        allVehicles           -> home screen count + available list
--    VehicleService.GetAllVehiclesAsync
--        -> vehicle list page, expense form, document form
--    TravelManagement_API/Controllers/BulkUploadController.cs
--        lines 239, 327, 375, 450  -> name->id maps
--
--  And INVERT it on the rental-rates screen so it shows only the
--  catalog:  v."VehicleNumber" LIKE 'RENTAL-%'
-- =====================================================================


-- =====================================================================
--  UNDO — removes the catalog and its rates, leaves real vehicles alone
-- =====================================================================
-- BEGIN;
-- DELETE FROM "RentalRates" rr
--  USING "Vehicles" v
--  WHERE rr."VehicleId" = v."VehicleId"
--    AND v."VehicleNumber" LIKE 'RENTAL-%' AND v."OrgId" = 1;
-- DELETE FROM "Vehicles"
--  WHERE "VehicleNumber" LIKE 'RENTAL-%' AND "OrgId" = 1;
-- COMMIT;
