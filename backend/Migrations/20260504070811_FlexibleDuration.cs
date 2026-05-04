using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiamondBooking.Migrations
{
    /// <inheritdoc />
    public partial class FlexibleDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "Services");

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "Services",
                newName: "PricePerHour");

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 60);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "Bookings");

            migrationBuilder.RenameColumn(
                name: "PricePerHour",
                table: "Services",
                newName: "Price");

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
