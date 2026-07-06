using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TravelManagement_DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalRatesAndReturnDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ReturnDate",
                table: "Bookings",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RentalRates",
                columns: table => new
                {
                    RentalRateId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    PricePerDay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WeeklyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SecurityDeposit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinDays = table.Column<int>(type: "integer", nullable: false),
                    KmPerDay = table.Column<int>(type: "integer", nullable: true),
                    ExtraKmRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    OrgId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalRates", x => x.RentalRateId);
                    table.ForeignKey(
                        name: "FK_RentalRates_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RentalRates_VehicleId_OrgId",
                table: "RentalRates",
                columns: new[] { "VehicleId", "OrgId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RentalRates");

            migrationBuilder.DropColumn(
                name: "ReturnDate",
                table: "Bookings");
        }
    }
}
