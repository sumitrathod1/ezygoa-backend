using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TravelManagement_DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgLinksAndBookingTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TransferredAt",
                table: "Bookings",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransferredFromOrgId",
                table: "Bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrgLinks",
                columns: table => new
                {
                    OrgLinkId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrgAId = table.Column<int>(type: "integer", nullable: false),
                    OrgBId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgLinks", x => x.OrgLinkId);
                    table.CheckConstraint("CK_OrgLinks_Order", "\"OrgAId\" < \"OrgBId\"");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_OrgId_travelDate",
                table: "Bookings",
                columns: new[] { "OrgId", "travelDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgLinks_OrgAId_OrgBId",
                table: "OrgLinks",
                columns: new[] { "OrgAId", "OrgBId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrgLinks");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_OrgId_travelDate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "TransferredAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "TransferredFromOrgId",
                table: "Bookings");
        }
    }
}
