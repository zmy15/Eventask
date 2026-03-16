using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventask.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class SpecialDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpecialDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialDays", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpecialDays_Date",
                table: "SpecialDays",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpecialDays");
        }
    }
}
