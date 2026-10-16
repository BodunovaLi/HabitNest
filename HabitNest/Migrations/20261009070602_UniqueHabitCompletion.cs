using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitNest.Migrations
{
    public partial class UniqueHabitCompletion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HabitCompletions_HabitId",
                table: "HabitCompletions");

            migrationBuilder.CreateIndex(
                name: "IX_HabitCompletions_HabitId_CompletionDate",
                table: "HabitCompletions",
                columns: new[] { "HabitId", "CompletionDate" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HabitCompletions_HabitId_CompletionDate",
                table: "HabitCompletions");

            migrationBuilder.CreateIndex(
                name: "IX_HabitCompletions_HabitId",
                table: "HabitCompletions",
                column: "HabitId");
        }
    }
}
