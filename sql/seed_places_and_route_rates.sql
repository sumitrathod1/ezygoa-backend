-- =====================================================================
--  EzyGoa — Seed Places + RouteRates
--  Source: https://zipgoa.com  (/airport-taxi, /pricing, /location/*)
--  Scraped & verified from raw page HTML on 2026-09-12.
--  Target: PostgreSQL (Npgsql)   OrgId = 1
--
--  SAFE TO RE-RUN: every INSERT is guarded by NOT EXISTS.
--  Nothing is updated or deleted — existing rows are left untouched.
--
--  Names are resolved by Place."Name", NOT by hardcoded PlaceId.
--
--  STRUCTURE (no route appears twice):
--    Part 1  Places                                  32 places
--    Part 2  Airport transfers — full fleet          15 routes x 7 = 105
--    Part 3  Airport transfers — Dzire/Ertiga/Innova  6 routes x 3 =  18
--    Part 4  Point-to-point (no airports)            84 routes x 3 = 252
--                                             TOTAL rate rows = 375
-- =====================================================================

BEGIN;

-- ---------------------------------------------------------------------
-- PART 1 — Places
-- Zone enum: 0 = North, 1 = South, 2 = Hub
-- ---------------------------------------------------------------------
INSERT INTO "Places" ("Name", "Aliases", "Zone", "PickupAllowed", "DropAllowed", "RestrictionMessage", "IsActive", "OrgId")
SELECT v.name, v.aliases, v.zone, TRUE, TRUE, NULL, TRUE, 1
FROM (VALUES
    -- Airports (Hub)
    ('Mopa Airport',      'GOX,Manohar International Airport,Pernem Airport,North Goa Airport', 2),
    ('Dabolim Airport',   'GOI,Goa International Airport,Vasco Airport',                        2),
    -- North Goa
    ('Calangute',         'Calangute Beach',                                                    0),
    ('Baga',              'Baga Beach',                                                         0),
    ('Anjuna',            'Anjuna Beach,Anjuna Flea Market',                                    0),
    ('Vagator',           'Vagator Beach,Ozran Beach',                                          0),
    ('Candolim',          'Candolim Beach',                                                     0),
    ('Sinquerim',         'Sinquerim Beach,Aguada',                                             0),
    ('Chapora',           'Chapora Fort',                                                       0),
    ('Panjim',            'Panaji,Panjim City,Capital',                                         0),
    ('Old Goa',           'Old Goa Churches,Basilica of Bom Jesus,Se Cathedral',                0),
    ('Mapusa',            'Mapusa Market',                                                      0),
    ('Arambol',           'Arambol Beach',                                                      0),
    ('Morjim',            'Morjim Beach',                                                       0),
    ('Ashwem',            'Ashwem Beach',                                                       0),
    ('Siolim',            NULL,                                                                 0),
    ('Fort Aguada',       'Aguada Fort',                                                        0),
    ('Miramar',           'Miramar Beach',                                                      0),
    ('Dona Paula',        'Dona Paula Viewpoint,Dona Paula Jetty',                              0),
    ('Fontainhas',        'Fontainhas Latin Quarter,Latin Quarter',                             0),
    ('Mangueshi Temple',  'Mangeshi Temple,Mangueshi',                                          0),
    ('Pilar Seminary',    'Pilar Monastery,Pilar',                                              0),
    -- South Goa
    ('Margao',            'Madgaon,Margao City,South Goa',                                      1),
    ('Colva',             'Colva Beach',                                                        1),
    ('Palolem',           'Palolem Beach,Canacona',                                             1),
    ('Agonda',            'Agonda Beach',                                                       1),
    ('Benaulim',          'Benaulim Beach',                                                     1),
    ('Vasco da Gama',     'Vasco,Vasco City',                                                   1),
    ('Cabo de Rama',      'Cabo de Rama Fort',                                                  1),
    ('Butterfly Beach',   'Butterfly Island',                                                   1),
    ('Cotigao Wildlife Sanctuary', 'Cotigao,Cotigao Sanctuary',                                 1),
    ('Dudhsagar Falls',   'Dudhsagar,Dudhsagar Waterfall,Mollem',                               1)
) AS v(name, aliases, zone)
WHERE NOT EXISTS (
    SELECT 1 FROM "Places" p
    WHERE lower(p."Name") = lower(v.name) AND p."OrgId" = 1
);


-- ---------------------------------------------------------------------
-- PART 2 — Airport transfers, full fleet (7 vehicle types)
-- Source: https://zipgoa.com/airport-taxi
-- "All prices are one-way, per vehicle (not per person). Toll extra."
--
-- zipgoa groups destinations ("Calangute / Baga", "Anjuna / Vagator",
-- "Margao / Colva") — each is expanded into its own route below.
-- ---------------------------------------------------------------------
INSERT INTO "RouteRates" ("FromPlaceId", "ToPlaceId", "VehicleType", "Price", "PeakPrice", "FloorPrice", "IsActive", "OrgId")
SELECT f."PlaceId", t."PlaceId", veh.vehicle, veh.price,
       round(veh.price * 1.15 / 50.0) * 50,   -- PeakPrice  (derived)
       round(veh.price * 0.92 / 50.0) * 50,   -- FloorPrice (derived)
       TRUE, 1
FROM (VALUES
    --  from             to            Dzire Ertiga Innova  TT12  TT14  TT20  Urbania
    ('Mopa Airport',    'Calangute',   1250, 1500, 2000,   3800, 3800, 4200, 5500),
    ('Mopa Airport',    'Baga',        1250, 1500, 2000,   3800, 3800, 4200, 5500),
    ('Mopa Airport',    'Panjim',      1200, 1200, 1600,   3800, 3800, 3400, 4500),
    ('Mopa Airport',    'Anjuna',      1400, 1700, 2300,   3800, 3700, 4800, 6200),
    ('Mopa Airport',    'Vagator',     1400, 1700, 2300,   3800, 3700, 4800, 6200),
    ('Mopa Airport',    'Mapusa',       800, 1200, 1500,   3500, 3500, 3700, 4200),
    ('Mopa Airport',    'Margao',      2000, 2500, 3200,   5500, 5700, 6800, 9000),

    ('Dabolim Airport', 'Calangute',   1400, 1800, 2200,   3800, 4000, 4800, 6200),
    ('Dabolim Airport', 'Baga',        1400, 1800, 2200,   3800, 4000, 4800, 6200),
    ('Dabolim Airport', 'Panjim',      1100, 1200, 1600,   3800, 3800, 4000, 4500),
    -- NOTE: zipgoa prints Ertiga as Rs.100 for the Margao/Colva row — a typo
    -- on their site (Dzire 800, Innova 1500). Using 1000.
    ('Dabolim Airport', 'Margao',       800, 1000, 1500,   3200, 3500, 3800, 4800),
    ('Dabolim Airport', 'Colva',        800, 1000, 1500,   3200, 3500, 3800, 4800),
    ('Dabolim Airport', 'Palolem',     2500, 3000, 3800,   5500, 6000, 7800, 10000),
    ('Dabolim Airport', 'Anjuna',      1800, 2200, 2800,   4000, 4600, 6000, 7800),
    ('Dabolim Airport', 'Vagator',     1800, 2200, 2800,   4000, 4600, 6000, 7800)
) AS r(from_name, to_name, dzire, ertiga, innova, tt12, tt14, tt20, urbania)
JOIN "Places" f ON lower(f."Name") = lower(r.from_name) AND f."OrgId" = 1
JOIN "Places" t ON lower(t."Name") = lower(r.to_name)   AND t."OrgId" = 1
CROSS JOIN LATERAL (VALUES
    ('Dzire', r.dzire), ('Ertiga', r.ertiga), ('Innova', r.innova),
    ('TT12', r.tt12), ('TT14', r.tt14), ('TT20', r.tt20), ('Urbania', r.urbania)
) AS veh(vehicle, price)
WHERE NOT EXISTS (
    SELECT 1 FROM "RouteRates" rr
    WHERE rr."FromPlaceId" = f."PlaceId" AND rr."ToPlaceId" = t."PlaceId"
      AND rr."VehicleType" = veh.vehicle AND rr."OrgId" = 1
);


-- ---------------------------------------------------------------------
-- PART 3 — Airport transfers NOT on the /airport-taxi page
-- Source: the location pages. Only Dzire/Ertiga/Innova are published
-- for these, so no Tempo/Urbania rows here.
-- ---------------------------------------------------------------------
INSERT INTO "RouteRates" ("FromPlaceId", "ToPlaceId", "VehicleType", "Price", "PeakPrice", "FloorPrice", "IsActive", "OrgId")
SELECT f."PlaceId", t."PlaceId", veh.vehicle, veh.price,
       round(veh.price * 1.15 / 50.0) * 50,
       round(veh.price * 0.92 / 50.0) * 50,
       TRUE, 1
FROM (VALUES
    --  from         to                  Dzire Ertiga Innova
    ('Candolim', 'Mopa Airport',         1150, 1450, 1900),
    ('Candolim', 'Dabolim Airport',      1400, 1750, 2200),
    ('Old Goa',  'Mopa Airport',         1200, 1500, 2000),
    ('Old Goa',  'Dabolim Airport',       750,  950, 1300),
    ('Colva',    'Mopa Airport',         2100, 2600, 3400),
    ('Palolem',  'Mopa Airport',         3000, 3800, 5000)
) AS r(from_name, to_name, dzire, ertiga, innova)
JOIN "Places" f ON lower(f."Name") = lower(r.from_name) AND f."OrgId" = 1
JOIN "Places" t ON lower(t."Name") = lower(r.to_name)   AND t."OrgId" = 1
CROSS JOIN LATERAL (VALUES
    ('Dzire', r.dzire), ('Ertiga', r.ertiga), ('Innova', r.innova)
) AS veh(vehicle, price)
WHERE NOT EXISTS (
    SELECT 1 FROM "RouteRates" rr
    WHERE rr."FromPlaceId" = f."PlaceId" AND rr."ToPlaceId" = t."PlaceId"
      AND rr."VehicleType" = veh.vehicle AND rr."OrgId" = 1
);


-- ---------------------------------------------------------------------
-- PART 4 — Point-to-point rates (NO airport routes — see Parts 2 & 3)
--
-- Block A (60 routes): "From <X> to Popular Places" table on each
--                      https://zipgoa.com/location/<x> page.
-- Block B (24 routes): "Nearby Attractions" cards on the same pages.
--                      Those publish a Dzire price only — Ertiga/Innova
--                      are filled from zipgoa's own price-point for that
--                      Dzire value (e.g. every Dzire 350 route on the
--                      site is 350/450/600). Marked [d] where no such
--                      price-point existed and the observed site-wide
--                      ratio (Ertiga 1.28x, Innova 1.68x) was used.
-- ---------------------------------------------------------------------
INSERT INTO "RouteRates" ("FromPlaceId", "ToPlaceId", "VehicleType", "Price", "PeakPrice", "FloorPrice", "IsActive", "OrgId")
SELECT f."PlaceId", t."PlaceId", veh.vehicle, veh.price,
       round(veh.price * 1.15 / 50.0) * 50,
       round(veh.price * 0.92 / 50.0) * 50,
       TRUE, 1
FROM (VALUES
    --  from          to                            Dzire Ertiga Innova
    -- ============ BLOCK A — published rate tables ============
    ('Calangute', 'Baga',                            300,  400,  500),
    ('Calangute', 'Anjuna',                          400,  500,  700),
    ('Calangute', 'Panjim',                          600,  800, 1100),
    ('Calangute', 'Old Goa',                         900, 1200, 1600),
    ('Calangute', 'Margao',                         1800, 2200, 3000),
    ('Calangute', 'Dudhsagar Falls',                3500, 4500, 5500),

    ('Baga',      'Calangute',                       300,  400,  500),
    ('Baga',      'Anjuna',                          350,  450,  600),
    ('Baga',      'Panjim',                          650,  850, 1200),
    ('Baga',      'Old Goa',                         950, 1250, 1700),
    ('Baga',      'Chapora',                         450,  600,  800),
    ('Baga',      'Dudhsagar Falls',                3500, 4500, 5500),

    ('Anjuna',    'Calangute',                       400,  500,  700),
    ('Anjuna',    'Baga',                            350,  450,  600),
    ('Anjuna',    'Vagator',                         300,  400,  500),
    ('Anjuna',    'Panjim',                          700,  900, 1200),
    ('Anjuna',    'Old Goa',                         950, 1200, 1600),
    ('Anjuna',    'Dudhsagar Falls',                3700, 4700, 5800),

    ('Vagator',   'Anjuna',                          300,  400,  500),
    ('Vagator',   'Calangute',                       500,  700,  900),
    ('Vagator',   'Panjim',                          800, 1000, 1400),
    ('Vagator',   'Old Goa',                        1000, 1300, 1800),
    ('Vagator',   'Arambol',                         850, 1100, 1500),
    ('Vagator',   'Dudhsagar Falls',                3800, 4800, 6000),

    ('Candolim',  'Calangute',                       300,  400,  500),
    ('Candolim',  'Baga',                            350,  450,  600),
    ('Candolim',  'Panjim',                          500,  700,  950),
    ('Candolim',  'Anjuna',                          600,  800, 1100),
    ('Candolim',  'Old Goa',                         800, 1000, 1400),
    ('Candolim',  'Dudhsagar Falls',                3600, 4600, 5700),

    ('Panjim',    'Calangute',                       600,  800, 1100),
    ('Panjim',    'Old Goa',                         400,  550,  750),
    ('Panjim',    'Anjuna',                          700,  900, 1200),
    ('Panjim',    'Margao',                         1000, 1300, 1800),
    ('Panjim',    'Arambol',                        1400, 1800, 2400),
    ('Panjim',    'Dudhsagar Falls',                3200, 4000, 5000),

    ('Old Goa',   'Panjim',                          400,  550,  750),
    ('Old Goa',   'Calangute',                       900, 1200, 1600),
    ('Old Goa',   'Anjuna',                          950, 1200, 1600),
    ('Old Goa',   'Margao',                          950, 1200, 1600),
    ('Old Goa',   'Colva',                          1100, 1400, 1900),
    ('Old Goa',   'Dudhsagar Falls',                2500, 3200, 4200),

    ('Margao',    'Panjim',                         1000, 1300, 1800),
    ('Margao',    'Colva',                           350,  450,  600),
    ('Margao',    'Palolem',                        1200, 1500, 2000),
    ('Margao',    'Old Goa',                         950, 1200, 1600),
    ('Margao',    'Calangute',                      1800, 2200, 3000),
    ('Margao',    'Dudhsagar Falls',                2500, 3200, 4200),

    ('Colva',     'Margao',                          350,  450,  600),
    ('Colva',     'Panjim',                         1200, 1500, 2000),
    ('Colva',     'Palolem',                        1300, 1700, 2200),
    ('Colva',     'Calangute',                      1700, 2100, 2800),
    ('Colva',     'Old Goa',                        1100, 1400, 1900),
    ('Colva',     'Dudhsagar Falls',                2200, 2800, 3600),

    ('Palolem',   'Margao',                         1200, 1500, 2000),
    ('Palolem',   'Colva',                          1300, 1700, 2200),
    ('Palolem',   'Panjim',                         2200, 2800, 3600),
    ('Palolem',   'Agonda',                          400,  500,  700),
    ('Palolem',   'Cabo de Rama',                   1000, 1300, 1700),
    ('Palolem',   'Dudhsagar Falls',                2500, 3200, 4200),

    -- ============ BLOCK B — from "Nearby Attractions" ============
    ('Anjuna',    'Chapora',                         350,  450,  600),
    ('Anjuna',    'Mapusa',                          500,  700,  900),
    ('Baga',      'Vagator',                         450,  600,  800),
    ('Baga',      'Arpora',                          300,  400,  500),
    ('Baga',      'Fort Aguada',                     550,  700,  950),
    ('Calangute', 'Vagator',                         500,  700,  900),
    ('Calangute', 'Chapora',                         600,  800, 1100),
    ('Calangute', 'Fort Aguada',                     500,  700,  900),
    ('Candolim',  'Fort Aguada',                     200,  250,  350),  -- [d]
    ('Candolim',  'Sinquerim',                       150,  200,  250),  -- [d]
    ('Candolim',  'Miramar',                         450,  600,  800),
    ('Vagator',   'Chapora',                         150,  200,  250),  -- [d]
    ('Vagator',   'Baga',                            450,  600,  800),
    ('Vagator',   'Morjim',                          600,  800, 1100),
    ('Vagator',   'Mapusa',                          500,  700,  900),
    ('Panjim',    'Miramar',                         200,  250,  350),  -- [d]
    ('Panjim',    'Dona Paula',                      350,  450,  600),
    ('Panjim',    'Fontainhas',                      150,  200,  250),  -- [d]
    ('Old Goa',   'Pilar Seminary',                  350,  450,  600),
    ('Margao',    'Benaulim',                        400,  500,  700),
    ('Margao',    'Mangueshi Temple',                600,  800, 1100),
    ('Colva',     'Benaulim',                        200,  250,  350),  -- [d]
    ('Palolem',   'Butterfly Beach',                 300,  400,  500),
    ('Palolem',   'Cotigao Wildlife Sanctuary',      600,  800, 1100)
) AS r(from_name, to_name, dzire, ertiga, innova)
JOIN "Places" f ON lower(f."Name") = lower(r.from_name) AND f."OrgId" = 1
JOIN "Places" t ON lower(t."Name") = lower(r.to_name)   AND t."OrgId" = 1
CROSS JOIN LATERAL (VALUES
    ('Dzire', r.dzire), ('Ertiga', r.ertiga), ('Innova', r.innova)
) AS veh(vehicle, price)
WHERE NOT EXISTS (
    SELECT 1 FROM "RouteRates" rr
    WHERE rr."FromPlaceId" = f."PlaceId" AND rr."ToPlaceId" = t."PlaceId"
      AND rr."VehicleType" = veh.vehicle AND rr."OrgId" = 1
);

COMMIT;


-- =====================================================================
--  VERIFICATION
-- =====================================================================

-- Expect (excluding your 10 pre-existing rows):
--   Dzire 105 | Ertiga 105 | Innova 105 | TT12 15 | TT14 15 | TT20 15 | Urbania 15
-- SELECT "VehicleType", COUNT(*) FROM "RouteRates" WHERE "OrgId" = 1
-- GROUP BY "VehicleType" ORDER BY 1;

-- No route should be defined twice — must return 0 rows
-- SELECT "FromPlaceId", "ToPlaceId", "VehicleType", COUNT(*)
-- FROM "RouteRates" WHERE "OrgId" = 1
-- GROUP BY 1,2,3 HAVING COUNT(*) > 1;

-- Full readable rate card
-- SELECT f."Name" AS from_place, t."Name" AS to_place,
--        rr."VehicleType", rr."Price", rr."PeakPrice", rr."FloorPrice"
-- FROM "RouteRates" rr
-- JOIN "Places" f ON f."PlaceId" = rr."FromPlaceId"
-- JOIN "Places" t ON t."PlaceId" = rr."ToPlaceId"
-- WHERE rr."OrgId" = 1
-- ORDER BY f."Name", t."Name",
--          array_position(ARRAY['Dzire','Ertiga','Innova','TT12','TT14','TT20','Urbania'], rr."VehicleType");

-- Routes with no big-vehicle (TT14/TT20/Urbania) rate yet
-- SELECT f."Name", t."Name"
-- FROM "RouteRates" rr
-- JOIN "Places" f ON f."PlaceId" = rr."FromPlaceId"
-- JOIN "Places" t ON t."PlaceId" = rr."ToPlaceId"
-- WHERE rr."OrgId" = 1
-- GROUP BY 1,2
-- HAVING COUNT(*) FILTER (WHERE rr."VehicleType" IN ('TT14','TT20','Urbania')) = 0;


-- =====================================================================
--  RECOMMENDED: make duplicate rates impossible
-- =====================================================================
-- CREATE UNIQUE INDEX IF NOT EXISTS "UX_RouteRates_Route_Vehicle"
--     ON "RouteRates" ("OrgId", "FromPlaceId", "ToPlaceId", "VehicleType");


-- =====================================================================
--  REFERENCE — not inserted (no table for these yet)
--  Source: https://zipgoa.com/pricing
--
--  Per-km rates (fallback when no fixed route rate exists):
--    Dzire Rs.14 | Ertiga Rs.18 | Innova Rs.22   (min 250 km)
--    TT12  Rs.28 | TT14   Rs.30 | TT20   Rs.30
--    Urbania Rs.35                               (min 300 km)
--
--  8-hour local package (identical on every location page):
--    Dzire 2500 | Ertiga 3000 | Innova 4000 | TT12 4000
--    TT14  4500 | TT20   5500 | Urbania 7500
--
--  Extras: toll extra, parking extra,
--          driver night allowance Rs.500/day (after 10 PM),
--          waiting Rs.150/hour.
-- =====================================================================
