using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddNotifications : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                DedupKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                SourceId = table.Column<Guid>(type: "TEXT", nullable: true),
                ReferenceDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                SubjectLabel = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                Currency = table.Column<int>(type: "INTEGER", nullable: true),
                Percent = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                Tier = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                DigestFrequency = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                DigestPaymentCount = table.Column<int>(type: "INTEGER", nullable: true),
                DigestBudgetCount = table.Column<int>(type: "INTEGER", nullable: true),
                DigestAnomalyCount = table.Column<int>(type: "INTEGER", nullable: true),
                DigestTotals = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                ReadAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                DismissedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Notifications", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_DedupKey",
            table: "Notifications",
            column: "DedupKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_DismissedAt",
            table: "Notifications",
            column: "DismissedAt");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_ReferenceDate",
            table: "Notifications",
            column: "ReferenceDate");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Notifications");
    }
}
