using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddGoals : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Goals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                TargetAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                TargetAmountCurrency = table.Column<int>(type: "INTEGER", nullable: false),
                Currency = table.Column<int>(type: "INTEGER", nullable: false),
                TargetDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                Status = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Goals", x => x.Id));

        migrationBuilder.CreateTable(
            name: "GoalContributions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalId = table.Column<Guid>(type: "TEXT", nullable: false),
                Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                AmountCurrency = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GoalContributions", x => x.Id);
                table.ForeignKey(
                    name: "FK_GoalContributions_Goals_GoalId",
                    column: x => x.GoalId,
                    principalTable: "Goals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GoalContributions_GoalId",
            table: "GoalContributions",
            column: "GoalId");

        migrationBuilder.CreateIndex(
            name: "IX_Goals_Status",
            table: "Goals",
            column: "Status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "GoalContributions");

        migrationBuilder.DropTable(
            name: "Goals");
    }
}
