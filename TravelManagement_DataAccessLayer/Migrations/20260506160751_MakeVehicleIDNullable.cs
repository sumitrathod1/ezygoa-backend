using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelManagement_DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class MakeVehicleIDNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_vehicleExpences_Vehicles_VehicleID",
                table: "vehicleExpences");

            migrationBuilder.AlterColumn<int>(
                name: "VehicleID",
                table: "vehicleExpences",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_vehicleExpences_Vehicles_VehicleID",
                table: "vehicleExpences",
                column: "VehicleID",
                principalTable: "Vehicles",
                principalColumn: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_vehicleExpences_Vehicles_VehicleID",
                table: "vehicleExpences");

            migrationBuilder.AlterColumn<int>(
                name: "VehicleID",
                table: "vehicleExpences",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_vehicleExpences_Vehicles_VehicleID",
                table: "vehicleExpences",
                column: "VehicleID",
                principalTable: "Vehicles",
                principalColumn: "VehicleId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
