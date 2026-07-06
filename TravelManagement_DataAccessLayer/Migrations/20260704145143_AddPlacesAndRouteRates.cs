using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TravelManagement_DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddPlacesAndRouteRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Places",
                columns: table => new
                {
                    PlaceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Aliases = table.Column<string>(type: "text", nullable: true),
                    Zone = table.Column<int>(type: "integer", nullable: false),
                    PickupAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    DropAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    RestrictionMessage = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    OrgId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Places", x => x.PlaceId);
                });

            migrationBuilder.CreateTable(
                name: "RouteRates",
                columns: table => new
                {
                    RouteRateId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FromPlaceId = table.Column<int>(type: "integer", nullable: false),
                    ToPlaceId = table.Column<int>(type: "integer", nullable: false),
                    VehicleType = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PeakPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    FloorPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    OrgId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteRates", x => x.RouteRateId);
                    table.ForeignKey(
                        name: "FK_RouteRates_Places_FromPlaceId",
                        column: x => x.FromPlaceId,
                        principalTable: "Places",
                        principalColumn: "PlaceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RouteRates_Places_ToPlaceId",
                        column: x => x.ToPlaceId,
                        principalTable: "Places",
                        principalColumn: "PlaceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Places",
                columns: new[] { "PlaceId", "Aliases", "DropAllowed", "IsActive", "Name", "OrgId", "PickupAllowed", "RestrictionMessage", "Zone" },
                values: new object[,]
                {
                    { 1, "Vagator Beach", true, true, "Vagator", 1, true, null, 0 },
                    { 2, "Chapora Fort", true, true, "Chapora", 1, true, null, 0 },
                    { 3, "Anjuna Beach,Anjuna Flea Market", true, true, "Anjuna", 1, true, null, 0 },
                    { 4, "Baga Beach", true, true, "Baga", 1, true, null, 0 },
                    { 5, "Calangute Beach", true, true, "Calangute", 1, true, null, 0 },
                    { 6, "Candolim Beach", true, true, "Candolim", 1, true, null, 0 },
                    { 7, "Sinquerim Beach,Aguada", true, true, "Sinquerim", 1, true, null, 0 },
                    { 8, null, true, true, "Arpora", 1, true, null, 0 },
                    { 9, null, true, true, "Saligao", 1, true, null, 0 },
                    { 10, null, true, true, "Parra", 1, true, null, 0 },
                    { 11, "Mapusa Market", true, true, "Mapusa", 1, true, null, 0 },
                    { 12, null, true, true, "Guirim", 1, true, null, 0 },
                    { 13, null, true, true, "Porvorim", 1, true, null, 0 },
                    { 14, null, true, true, "Nerul", 1, true, null, 0 },
                    { 15, "Panaji,Panaji City,Panjim City,Goa Capital", true, true, "Panjim", 1, true, null, 0 },
                    { 16, "Miramar Beach", true, true, "Miramar", 1, true, null, 0 },
                    { 17, "Dona Paula Beach,Dona Paula Jetty", true, true, "Dona Paula", 1, true, null, 0 },
                    { 18, null, true, true, "Taleigao", 1, true, null, 0 },
                    { 19, "Bambolim Beach,Goa University", true, true, "Bambolim", 1, true, null, 0 },
                    { 20, "GOX,Mopa,Manohar International Airport,North Goa Airport", true, true, "Mopa Airport", 1, true, null, 2 },
                    { 21, "GOI,Dabolim,Goa International Airport,Vasco Airport", true, true, "Dabolim Airport", 1, true, null, 2 },
                    { 22, "Thivim Station,Thivim Railway Station", true, true, "Thivim Railway", 1, true, null, 2 },
                    { 23, "Karmali Station,Old Goa Station,Karmali", true, true, "Karmali Railway", 1, true, null, 2 },
                    { 24, "Margao Railway,Margao Station,Madgaon Station,Margao", true, true, "Madgaon Railway", 1, true, null, 2 },
                    { 25, "Vasco da Gama Railway,Vasco Station,Vasco", true, true, "Vasco Railway", 1, true, null, 2 },
                    { 26, "Colva Beach", true, true, "Colva", 1, false, "We can drop you here but pickup from this area is outside our zone. Nearest pickup points: Madgaon Railway Station or Dabolim Airport.", 1 },
                    { 27, "Majorda Beach", true, true, "Majorda", 1, false, "We can drop you here but pickup from this area is outside our zone. Nearest pickup points: Madgaon Railway Station or Dabolim Airport.", 1 },
                    { 28, "Benaulim Beach", true, true, "Benaulim", 1, false, "We can drop you here but pickup from this area is outside our zone. Nearest pickup points: Madgaon Railway Station or Dabolim Airport.", 1 },
                    { 29, "Varca Beach", true, true, "Varca", 1, false, "We can drop you here but pickup from this area is outside our zone. Nearest pickup points: Madgaon Railway Station or Dabolim Airport.", 1 },
                    { 30, "Palolem Beach", true, true, "Palolem", 1, false, "We can drop you here but pickup from this area is outside our zone. Nearest pickup points: Madgaon Railway Station or Dabolim Airport.", 1 },
                    { 31, "Agonda Beach", true, true, "Agonda", 1, false, "We can drop you here but pickup from this area is outside our zone. Nearest pickup points: Madgaon Railway Station or Dabolim Airport.", 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_RouteRates_FromPlaceId_ToPlaceId_VehicleType_OrgId",
                table: "RouteRates",
                columns: new[] { "FromPlaceId", "ToPlaceId", "VehicleType", "OrgId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RouteRates_ToPlaceId",
                table: "RouteRates",
                column: "ToPlaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RouteRates");

            migrationBuilder.DropTable(
                name: "Places");
        }
    }
}
