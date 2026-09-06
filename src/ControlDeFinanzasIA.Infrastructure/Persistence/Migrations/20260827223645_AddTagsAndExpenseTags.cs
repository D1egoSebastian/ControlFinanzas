using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTagsAndExpenseTags : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Tags",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                Color = table.Column<string>(type: "TEXT", maxLength: 9, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Tags", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ExpenseTags",
            columns: table => new
            {
                ExpenseId = table.Column<Guid>(type: "TEXT", nullable: false),
                TagId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExpenseTags", x => new { x.ExpenseId, x.TagId });
                table.ForeignKey(
                    name: "FK_ExpenseTags_Expenses_ExpenseId",
                    column: x => x.ExpenseId,
                    principalTable: "Expenses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ExpenseTags_Tags_TagId",
                    column: x => x.TagId,
                    principalTable: "Tags",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExpenseTags_TagId",
            table: "ExpenseTags",
            column: "TagId");

        migrationBuilder.CreateIndex(
            name: "IX_Tags_Name",
            table: "Tags",
            column: "Name",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ExpenseTags");

        migrationBuilder.DropTable(
            name: "Tags");
    }
}
