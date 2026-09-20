using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddBudgets : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Budgets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Year = table.Column<int>(type: "INTEGER", nullable: false),
                Month = table.Column<int>(type: "INTEGER", nullable: false),
                CategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                AmountCurrency = table.Column<int>(type: "INTEGER", nullable: false),
                Currency = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Budgets", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Budgets_Year_Month_CategoryId",
            table: "Budgets",
            columns: new[] { "Year", "Month", "CategoryId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Budgets");
    }
}
