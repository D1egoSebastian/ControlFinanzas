using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRecurringRules : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RecurringRules",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                AmountCurrency = table.Column<int>(type: "INTEGER", nullable: false),
                Currency = table.Column<int>(type: "INTEGER", nullable: false),
                Frequency = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                PaymentMethodId = table.Column<Guid>(type: "TEXT", nullable: false),
                TagIds = table.Column<string>(type: "TEXT", nullable: false),
                Active = table.Column<bool>(type: "INTEGER", nullable: false),
                NextGenerationDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                RuleType = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_RecurringRules", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_RecurringRules_Active",
            table: "RecurringRules",
            column: "Active");

        migrationBuilder.CreateIndex(
            name: "IX_RecurringRules_NextGenerationDate",
            table: "RecurringRules",
            column: "NextGenerationDate");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RecurringRules");
    }
}
