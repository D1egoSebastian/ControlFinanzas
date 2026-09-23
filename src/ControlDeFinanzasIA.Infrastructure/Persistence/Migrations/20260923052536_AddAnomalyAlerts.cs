using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAnomalyAlerts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AnomalyAlerts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                TransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                Severity = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                AcknowledgedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_AnomalyAlerts", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_AnomalyAlerts_Date",
            table: "AnomalyAlerts",
            column: "Date");

        migrationBuilder.CreateIndex(
            name: "IX_AnomalyAlerts_TransactionId",
            table: "AnomalyAlerts",
            column: "TransactionId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AnomalyAlerts");
    }
}
