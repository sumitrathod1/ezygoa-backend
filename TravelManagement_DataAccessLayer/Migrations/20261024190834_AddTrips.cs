using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TravelManagement_DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddTrips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TripId",
                table: "Bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    TripId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrgId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    StayName = table.Column<string>(type: "text", nullable: true),
                    StayLat = table.Column<double>(type: "double precision", nullable: true),
                    StayLng = table.Column<double>(type: "double precision", nullable: true),
                    StayMapsLink = table.Column<string>(type: "text", nullable: true),
                    Pax = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.TripId);
                    table.ForeignKey(
                        name: "FK_Trips_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomersId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TripId",
                table: "Bookings",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_CustomerId",
                table: "Trips",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_OrgId_CustomerId_EndDate",
                table: "Trips",
                columns: new[] { "OrgId", "CustomerId", "EndDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Trips_TripId",
                table: "Bookings",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "TripId",
                onDelete: ReferentialAction.SetNull);

            // Existing packages become trips. The old form saved a package as one booking per
            // day in a single save, so a package is a run of bookings of the same customer with
            // consecutive IDs AND consecutive travel dates. Each run of 2+ gets one trip.
            migrationBuilder.Sql(TripBackfillSql);
        }

        /// <summary>Also run by the integration tests against legacy rows.</summary>
        public const string TripBackfillSql = @"
-- TRIP BACKFILL START
DO $$
DECLARE r RECORD; new_id integer;
BEGIN
  FOR r IN
    WITH ordered AS (
      SELECT b.""BookingId"", b.""OrgId"", b.""CustomerID"", b.""travelDate"", b.""Pax"",
             LAG(b.""BookingId"")  OVER w AS prev_id,
             LAG(b.""travelDate"") OVER w AS prev_date
      FROM ""Bookings"" b
      WHERE b.""TripId"" IS NULL
        AND EXISTS (SELECT 1 FROM ""Customers"" c WHERE c.""CustomersId"" = b.""CustomerID"")
      WINDOW w AS (PARTITION BY b.""OrgId"", b.""CustomerID"" ORDER BY b.""BookingId"")
    ),
    flagged AS (
      SELECT *, CASE WHEN prev_id = ""BookingId"" - 1 AND prev_date = ""travelDate"" - 1 THEN 0 ELSE 1 END AS is_start
      FROM ordered
    ),
    grouped AS (
      SELECT *, SUM(is_start) OVER (PARTITION BY ""OrgId"", ""CustomerID"" ORDER BY ""BookingId"") AS grp
      FROM flagged
    )
    SELECT ""OrgId"", ""CustomerID"", MIN(""travelDate"") AS s, MAX(""travelDate"") AS e,
           MAX(""Pax"") AS pax, array_agg(""BookingId"") AS ids
    FROM grouped
    GROUP BY ""OrgId"", ""CustomerID"", grp
    HAVING COUNT(*) >= 2
  LOOP
    INSERT INTO ""Trips"" (""OrgId"", ""CustomerId"", ""Pax"", ""StartDate"", ""EndDate"", ""CreatedAt"")
    VALUES (r.""OrgId"", r.""CustomerID"", COALESCE(r.pax, 0), r.s, r.e, now() AT TIME ZONE 'utc')
    RETURNING ""TripId"" INTO new_id;
    UPDATE ""Bookings"" SET ""TripId"" = new_id WHERE ""BookingId"" = ANY(r.ids);
  END LOOP;
END $$;
-- TRIP BACKFILL END
";

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Trips_TripId",
                table: "Bookings");

            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_TripId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "TripId",
                table: "Bookings");
        }
    }
}
