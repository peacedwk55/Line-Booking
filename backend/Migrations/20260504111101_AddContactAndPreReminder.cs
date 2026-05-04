using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiamondBooking.Migrations
{
    /// <inheritdoc />
    public partial class AddContactAndPreReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                table: "Bookings",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "Bookings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PreReminderSent",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactName",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PreReminderSent",
                table: "Bookings");
        }
    }
}
