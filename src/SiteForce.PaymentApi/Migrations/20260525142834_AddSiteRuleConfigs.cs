using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteForce.PaymentApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteRuleConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteRuleConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SiteName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AdvanceDeductionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SiteAllowancePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DisputeThresholdAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteRuleConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteRuleConfigs_SiteName",
                table: "SiteRuleConfigs",
                column: "SiteName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteRuleConfigs");
        }
    }
}
