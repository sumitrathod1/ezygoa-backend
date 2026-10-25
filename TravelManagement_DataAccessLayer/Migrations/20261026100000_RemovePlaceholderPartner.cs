using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TravelManagement.DataAccessLayer.Entities;

#nullable disable

namespace TravelManagement_DataAccessLayer.Migrations
{
    /// <summary>
    /// Data fix, no schema change. Bookings created through the API with Swagger's default
    /// value were linked to a "partner" named "string": no car / driver, no commission, so
    /// ₹0 earned (398 own trips in 2025). Unlink them so they count as own bookings again.
    /// Only bookings without a partner settlement are touched (real partner bookings always
    /// have one). The old values are kept in "_PlaceholderPartnerCleanup" so Down can undo it.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261026100000_RemovePlaceholderPartner")]
    public partial class RemovePlaceholderPartner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CleanupSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UndoSql);
        }

        public const string CleanupSql = @"
CREATE TABLE IF NOT EXISTS ""_PlaceholderPartnerCleanup"" (
    ""BookingId""          integer PRIMARY KEY,
    ""ExternalEmployeeId"" integer NOT NULL,
    ""Payment""            integer NOT NULL,
    ""CleanedAt""          timestamp without time zone NOT NULL DEFAULT now()
);

INSERT INTO ""_PlaceholderPartnerCleanup"" (""BookingId"", ""ExternalEmployeeId"", ""Payment"")
SELECT b.""BookingId"", b.""ExternalEmployeeId"", b.""Payment""
FROM ""Bookings"" b
JOIN ""ExternalEmployees"" e ON e.""externalEmployeeID"" = b.""ExternalEmployeeId""
WHERE LOWER(BTRIM(COALESCE(e.""externalEmployeeName"", ''))) = 'string'
  AND NOT EXISTS (SELECT 1 FROM ""ExternalEmployeeCashCollections"" s WHERE s.""BookingId"" = b.""BookingId"")
ON CONFLICT (""BookingId"") DO NOTHING;

-- Payment 0 = Admin (the business collects the fare)
UPDATE ""Bookings"" b
SET ""ExternalEmployeeId"" = NULL, ""Payment"" = 0
FROM ""_PlaceholderPartnerCleanup"" c
WHERE c.""BookingId"" = b.""BookingId"" AND b.""ExternalEmployeeId"" = c.""ExternalEmployeeId"";
";

        public const string UndoSql = @"
UPDATE ""Bookings"" b
SET ""ExternalEmployeeId"" = c.""ExternalEmployeeId"", ""Payment"" = c.""Payment""
FROM ""_PlaceholderPartnerCleanup"" c
WHERE c.""BookingId"" = b.""BookingId"" AND b.""ExternalEmployeeId"" IS NULL;

DROP TABLE IF EXISTS ""_PlaceholderPartnerCleanup"";
";
    }
}
