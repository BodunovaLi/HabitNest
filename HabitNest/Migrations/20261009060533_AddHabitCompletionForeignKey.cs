using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitNest.Migrations
{
    public partial class AddHabitCompletionForeignKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_HabitCompletions_HabitId",
                table: "HabitCompletions",
                column: "HabitId");

            migrationBuilder.AddForeignKey(
                name: "FK_HabitCompletions_Habits_HabitId",
                table: "HabitCompletions",
                column: "HabitId",
                principalTable: "Habits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HabitCompletions_Habits_HabitId",
                table: "HabitCompletions");

            migrationBuilder.DropIndex(
                name: "IX_HabitCompletions_HabitId",
                table: "HabitCompletions");
        }
    }
}
