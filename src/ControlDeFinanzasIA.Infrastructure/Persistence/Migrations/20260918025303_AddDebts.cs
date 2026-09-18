using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDebts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // NOTE: This migration originally diffed against a stale
        // AppDbContextModelSnapshot.cs that predated migration
        // 20260912150000_AddRecurringExpenseProvenance (that migration
        // was hand-authored and never regenerated the snapshot). The
        // Expenses.RecurringRuleId / RecurringPeriod columns and their
        // unique index already exist as of that migration; re-adding
        // them here would fail with "duplicate column" on any database
        // (fresh or existing) that applies migrations in order. Only
        // the snapshot is corrected by this migration; no duplicate
        // Expenses DDL is emitted.
        migrationBuilder.CreateTable(
            name: "Debts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                OriginalAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                OriginalAmountCurrency = table.Column<int>(type: "INTEGER", nullable: false),
                Currency = table.Column<int>(type: "INTEGER", nullable: false),
                TotalInstallments = table.Column<int>(type: "INTEGER", nullable: false),
                InstallmentAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                InstallmentAmountCurrency = table.Column<int>(type: "INTEGER", nullable: false),
                Frequency = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                CounterpartyName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                Status = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Debts", x => x.Id));

        migrationBuilder.CreateTable(
            name: "DebtPayments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                DebtId = table.Column<Guid>(type: "TEXT", nullable: false),
                InstallmentNumber = table.Column<int>(type: "INTEGER", nullable: false),
                ScheduledDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                PaidDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                AmountCurrency = table.Column<int>(type: "INTEGER", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DebtPayments", x => x.Id);
                table.ForeignKey(
                    name: "FK_DebtPayments_Debts_DebtId",
                    column: x => x.DebtId,
                    principalTable: "Debts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DebtPayments_DebtId",
            table: "DebtPayments",
            column: "DebtId");

        migrationBuilder.CreateIndex(
            name: "IX_DebtPayments_DebtId_InstallmentNumber",
            table: "DebtPayments",
            columns: new[] { "DebtId", "InstallmentNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DebtPayments_Status",
            table: "DebtPayments",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_Debts_Status",
            table: "Debts",
            column: "Status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DebtPayments");

        migrationBuilder.DropTable(
            name: "Debts");
    }
}
