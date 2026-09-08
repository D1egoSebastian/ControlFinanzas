using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPaymentMethodIdToTransactions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "PaymentMethodId",
            table: "Incomes",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "PaymentMethodId",
            table: "Expenses",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Incomes_PaymentMethodId",
            table: "Incomes",
            column: "PaymentMethodId");

        migrationBuilder.CreateIndex(
            name: "IX_Expenses_PaymentMethodId",
            table: "Expenses",
            column: "PaymentMethodId");

        migrationBuilder.AddForeignKey(
            name: "FK_Expenses_PaymentMethods_PaymentMethodId",
            table: "Expenses",
            column: "PaymentMethodId",
            principalTable: "PaymentMethods",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_Incomes_PaymentMethods_PaymentMethodId",
            table: "Incomes",
            column: "PaymentMethodId",
            principalTable: "PaymentMethods",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        // Migrar filas existentes al metodo "Efectivo" (idempotente:
        // solo afecta filas con PaymentMethodId NULL). La subquery
        // usa LIMIT 1 para ser defensiva ante multiples seeds con
        // el mismo nombre. Si 'Efectivo' no existe (caso patologico
        // donde se borro del seed), las filas quedan NULL y la FK
        // nullable lo acepta.
        migrationBuilder.Sql(@"
            UPDATE Expenses
            SET PaymentMethodId = (
                SELECT Id FROM PaymentMethods WHERE Name = 'Efectivo' LIMIT 1
            )
            WHERE PaymentMethodId IS NULL;
        ");

        migrationBuilder.Sql(@"
            UPDATE Incomes
            SET PaymentMethodId = (
                SELECT Id FROM PaymentMethods WHERE Name = 'Efectivo' LIMIT 1
            )
            WHERE PaymentMethodId IS NULL;
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Expenses_PaymentMethods_PaymentMethodId",
            table: "Expenses");

        migrationBuilder.DropForeignKey(
            name: "FK_Incomes_PaymentMethods_PaymentMethodId",
            table: "Incomes");

        migrationBuilder.DropIndex(
            name: "IX_Incomes_PaymentMethodId",
            table: "Incomes");

        migrationBuilder.DropIndex(
            name: "IX_Expenses_PaymentMethodId",
            table: "Expenses");

        migrationBuilder.DropColumn(
            name: "PaymentMethodId",
            table: "Incomes");

        migrationBuilder.DropColumn(
            name: "PaymentMethodId",
            table: "Expenses");
    }
}
