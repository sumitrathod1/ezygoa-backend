-- =====================================================================
--  EzyGoa — Seed RentalRates (self-drive)
--  Source: https://zipgoa.com/self-drive   (embedded fleet JSON)
--  Scraped & verified from raw page HTML on 2026-09-12.
--  Target: PostgreSQL (Npgsql)   OrgId = 1
--
--  IMPORTANT — this differs from Places/RouteRates:
--  RentalRates."VehicleId" is a FK to YOUR OWN "Vehicles" table (real
--  cars with registration numbers). zipgoa's rate card cannot be
--  inserted blindly — a rate row only makes sense for a car you own.
--  So this script matches on "Vehicles"."VehicleName" and inserts a
--  rate ONLY for vehicles you already have.
--
--  Site-wide terms (apply to every car):
--    "Unlimited km within Goa"  -> KmPerDay = NULL, ExtraKmRate = NULL
--    "Minimum booking: 2 days"  -> MinDays = 2
--    No weekly rate published   -> WeeklyRate = NULL
--    Late return Rs.200/hour · home delivery North Goa Rs.200
--    Rental day = 9 AM to next day 9 AM · driver age 21+
-- =====================================================================


-- ---------------------------------------------------------------------
-- STEP 1 — Look at your fleet first. Run this ALONE and read the output.
-- ---------------------------------------------------------------------
-- SELECT v."VehicleId", v."VehicleName", v."VehicleNumber",
--        v."Seatingcapacity",
--        (rr."RentalRateId" IS NOT NULL) AS has_rate
-- FROM "Vehicles" v
-- LEFT JOIN "RentalRates" rr
--        ON rr."VehicleId" = v."VehicleId" AND rr."OrgId" = 1
-- WHERE v."OrgId" = 1
-- ORDER BY v."VehicleId";


-- ---------------------------------------------------------------------
-- STEP 2 — Insert rates for every vehicle whose name matches the card.
--
-- Matching: "VehicleName" ILIKE a pattern, plus transmission inferred
-- from whether the name contains "auto". DISTINCT ON guarantees at most
-- one rate per vehicle (most specific pattern wins).
--
-- Vehicles that match nothing are simply skipped — see STEP 3.
-- ---------------------------------------------------------------------
INSERT INTO "RentalRates"
    ("VehicleId", "PricePerDay", "WeeklyRate", "SecurityDeposit",
     "MinDays", "KmPerDay", "ExtraKmRate", "IsActive", "OrgId")
SELECT DISTINCT ON (v."VehicleId")
       v."VehicleId",
       rc.price,
       NULL,              -- WeeklyRate   — not published by zipgoa
       rc.deposit,
       2,                 -- MinDays      — "Minimum booking: 2 days"
       NULL,              -- KmPerDay     — "Unlimited km within Goa"
       NULL,              -- ExtraKmRate  — unlimited, so no extra-km rate
       TRUE,
       1
FROM "Vehicles" v
JOIN (VALUES
    --  pattern            exclude        transmission  price  deposit
    ('%swift%',          '%dzire%',      'Manual',     1400,  3000),
    ('%swift%',          '%dzire%',      'Automatic',  1600,  3500),
    ('%baleno%',          NULL,          'Manual',     1500,  3000),
    ('%baleno%',          NULL,          'Automatic',  1800,  4000),
    ('%i20%',             NULL,          'Manual',     1500,  3500),
    ('%i20%',             NULL,          'Automatic',  1800,  4000),
    ('%dzire%',           NULL,          'Manual',     1500,  3500),
    ('%dzire%',           NULL,          'Automatic',  1800,  4000),
    ('%ertiga%',          NULL,          'Manual',     2200,  5000),
    ('%ertiga%',          NULL,          'Automatic',  2800,  6000),
    ('%creta%',           NULL,          'Manual',     2500,  5000),
    ('%creta%',           NULL,          'Automatic',  3500,  6000),
    ('%innova%',         '%hycross%',    'Manual',     3000,  6000),
    ('%innova%',         '%hycross%',    'Automatic',  3500,  7000),
    ('%hycross%',         NULL,          'Automatic',  4000,  8000),
    ('%thar%',           '%convert%',    'Manual',     3500,  7000),
    ('%thar%',           '%convert%',    'Automatic',  3500,  7000),
    ('%thar%convert%',    NULL,          'Manual',     3500,  7000),
    ('%thar%convert%',    NULL,          'Automatic',  4000,  8000)
) AS rc(pattern, exclude, trans, price, deposit)
  ON  v."VehicleName" ILIKE rc.pattern
  AND (rc.exclude IS NULL OR v."VehicleName" NOT ILIKE rc.exclude)
  AND (CASE WHEN rc.trans = 'Automatic'
            THEN v."VehicleName" ILIKE '%auto%'
            ELSE v."VehicleName" NOT ILIKE '%auto%'
       END)
WHERE v."OrgId" = 1
  AND NOT EXISTS (
      SELECT 1 FROM "RentalRates" rr
      WHERE rr."VehicleId" = v."VehicleId" AND rr."OrgId" = 1
  )
ORDER BY v."VehicleId", length(rc.pattern) DESC;


-- ---------------------------------------------------------------------
-- STEP 3 — Which of your vehicles got no rate? (expected: taxis,
--          Tempo Travellers, Urbania — zipgoa self-drive is cars only)
-- ---------------------------------------------------------------------
-- SELECT v."VehicleId", v."VehicleName"
-- FROM "Vehicles" v
-- LEFT JOIN "RentalRates" rr
--        ON rr."VehicleId" = v."VehicleId" AND rr."OrgId" = 1
-- WHERE v."OrgId" = 1 AND rr."RentalRateId" IS NULL
-- ORDER BY v."VehicleId";


-- ---------------------------------------------------------------------
-- STEP 4 — Verify what was written
-- ---------------------------------------------------------------------
-- SELECT v."VehicleId", v."VehicleName", rr."PricePerDay",
--        rr."SecurityDeposit", rr."MinDays", rr."KmPerDay", rr."ExtraKmRate"
-- FROM "RentalRates" rr
-- JOIN "Vehicles" v ON v."VehicleId" = rr."VehicleId"
-- WHERE rr."OrgId" = 1
-- ORDER BY rr."PricePerDay";


-- =====================================================================
--  MANUAL FALLBACK
--  If a car of yours is named in a way the patterns miss, insert it
--  directly — replace <VehicleId>, and take price/deposit from the
--  reference card at the bottom of this file.
-- =====================================================================
-- INSERT INTO "RentalRates"
--     ("VehicleId", "PricePerDay", "WeeklyRate", "SecurityDeposit",
--      "MinDays", "KmPerDay", "ExtraKmRate", "IsActive", "OrgId")
-- VALUES (<VehicleId>, <price>, NULL, <deposit>, 2, NULL, NULL, TRUE, 1);


-- =====================================================================
--  REFERENCE — zipgoa self-drive rate card, all 19 variants
--  (verified from the fleet JSON embedded in /self-drive)
--
--  CAR                                      TYPE       TRANS   SEATS  /DAY  DEPOSIT
--  Maruti Swift New                         Hatchback  Manual      5  1400     3000
--  Maruti Baleno New                        Hatchback  Manual      5  1500     3000
--  Hyundai i20 New                          Hatchback  Manual      5  1500     3500
--  Swift Dzire New                          Sedan      Manual      5  1500     3500
--  Maruti Swift New                         Hatchback  Auto        5  1600     3500
--  Maruti Baleno New                        Hatchback  Auto        5  1800     4000
--  Hyundai i20 New                          Hatchback  Auto        5  1800     4000
--  Swift Dzire New                          Sedan      Auto        5  1800     4000
--  Maruti Ertiga New 7-Seater               MPV        Manual      7  2200     5000
--  Hyundai Creta New                        SUV        Manual      5  2500     5000
--  Maruti Ertiga New 7-Seater               MPV        Auto        7  2800     6000
--  Toyota Innova Crysta                     MPV        Manual      7  3000     6000
--  Hyundai Creta New (Panoramic Sunroof)    SUV        Auto        5  3500     6000
--  Toyota Innova Crysta                     MPV        Auto        7  3500     7000
--  Mahindra Thar New Hard Top               4x4 SUV    Manual      4  3500     7000
--  Mahindra Thar New Hard Top               4x4 SUV    Auto        4  3500     7000
--  Mahindra Thar Convertible 4x4 Soft Top   4x4 Conv   Manual      4  3500     7000
--  Toyota Innova Hycross Hybrid             MPV        Auto        7  4000     8000
--  Mahindra Thar Convertible 4x4 Soft Top   4x4 Conv   Auto        4  4000     8000
--
--  Included : 24/7 roadside assistance, comprehensive insurance,
--             sanitized vehicle, full fuel tank on delivery,
--             unlimited km within Goa
--  Excluded : fuel (return full), tolls, parking, traffic fines,
--             damage beyond normal wear
--  Docs     : original Indian DL + Aadhaar/Passport, age 21+
-- =====================================================================
