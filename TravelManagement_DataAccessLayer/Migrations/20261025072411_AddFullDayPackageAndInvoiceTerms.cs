using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TravelManagement_DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddFullDayPackageAndInvoiceTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RateCategory",
                table: "Vehicles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FullDayHours",
                table: "Bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InvoiceSettings",
                columns: table => new
                {
                    InvoiceSettingsId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrgId = table.Column<int>(type: "integer", nullable: false),
                    NightChargesJson = table.Column<string>(type: "text", nullable: false),
                    ExtraRatesJson = table.Column<string>(type: "text", nullable: false),
                    OtherTerms = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceSettings", x => x.InvoiceSettingsId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSettings_OrgId",
                table: "InvoiceSettings",
                column: "OrgId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceSettings");

            migrationBuilder.DropColumn(
                name: "RateCategory",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "FullDayHours",
                table: "Bookings");
        }
    }
}
