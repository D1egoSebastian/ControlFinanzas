using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSavedViewsTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SavedViews",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                EntityType = table.Column<int>(type: "INTEGER", nullable: false),
                FilterJson = table.Column<string>(type: "TEXT", nullable: false),
                SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SavedViews", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_SavedViews_EntityType",
            table: "SavedViews",
            column: "EntityType");

        migrationBuilder.CreateIndex(
            name: "IX_SavedViews_Name",
            table: "SavedViews",
            column: "Name",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SavedViews");
    }
}
