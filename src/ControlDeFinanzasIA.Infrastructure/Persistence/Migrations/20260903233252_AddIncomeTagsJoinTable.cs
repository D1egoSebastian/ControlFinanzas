using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddIncomeTagsJoinTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "IncomeTags",
            columns: table => new
            {
                IncomeId = table.Column<Guid>(type: "TEXT", nullable: false),
                TagId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IncomeTags", x => new { x.IncomeId, x.TagId });
                table.ForeignKey(
                    name: "FK_IncomeTags_Incomes_IncomeId",
                    column: x => x.IncomeId,
                    principalTable: "Incomes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_IncomeTags_Tags_TagId",
                    column: x => x.TagId,
                    principalTable: "Tags",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_IncomeTags_TagId",
            table: "IncomeTags",
            column: "TagId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "IncomeTags");
    }
}
