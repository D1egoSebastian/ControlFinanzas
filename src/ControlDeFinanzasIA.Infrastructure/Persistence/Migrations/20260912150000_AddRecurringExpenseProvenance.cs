using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Migrations;

public partial class AddRecurringExpenseProvenance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("RecurringRuleId", "Expenses", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<DateOnly>("RecurringPeriod", "Expenses", type: "TEXT", nullable: true);
        migrationBuilder.CreateIndex("IX_Expenses_RecurringRuleId_RecurringPeriod", "Expenses", new[] { "RecurringRuleId", "RecurringPeriod" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_Expenses_RecurringRuleId_RecurringPeriod", "Expenses");
        migrationBuilder.DropColumn("RecurringRuleId", "Expenses");
        migrationBuilder.DropColumn("RecurringPeriod", "Expenses");
    }
}
