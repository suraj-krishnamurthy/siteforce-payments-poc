using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteForce.PaymentApi.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvanceTaken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdvanceTaken",
                table: "AttendanceRecords",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdvanceTaken",
                table: "AttendanceRecords");
        }
    }
}
