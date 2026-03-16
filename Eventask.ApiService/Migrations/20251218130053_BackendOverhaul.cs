using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventask.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class BackendOverhaul : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecurrenceRule_ScheduleItems_ScheduleItemId",
                table: "RecurrenceRule");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RecurrenceRule",
                table: "RecurrenceRule");

            migrationBuilder.RenameTable(
                name: "RecurrenceRule",
                newName: "RecurrenceRules");

            migrationBuilder.RenameIndex(
                name: "IX_RecurrenceRule_ScheduleItemId",
                table: "RecurrenceRules",
                newName: "IX_RecurrenceRules_ScheduleItemId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RecurrenceRules",
                table: "RecurrenceRules",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RecurrenceRules_ScheduleItems_ScheduleItemId",
                table: "RecurrenceRules",
                column: "ScheduleItemId",
                principalTable: "ScheduleItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecurrenceRules_ScheduleItems_ScheduleItemId",
                table: "RecurrenceRules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RecurrenceRules",
                table: "RecurrenceRules");

            migrationBuilder.RenameTable(
                name: "RecurrenceRules",
                newName: "RecurrenceRule");

            migrationBuilder.RenameIndex(
                name: "IX_RecurrenceRules_ScheduleItemId",
                table: "RecurrenceRule",
                newName: "IX_RecurrenceRule_ScheduleItemId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RecurrenceRule",
                table: "RecurrenceRule",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RecurrenceRule_ScheduleItems_ScheduleItemId",
                table: "RecurrenceRule",
                column: "ScheduleItemId",
                principalTable: "ScheduleItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
