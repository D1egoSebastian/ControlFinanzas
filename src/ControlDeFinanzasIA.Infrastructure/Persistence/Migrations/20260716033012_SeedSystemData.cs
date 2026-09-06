using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class SeedSystemData : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            table: "Categories",
            columns: new[] { "Id", "Color", "CreatedAt", "Icon", "IsSystem", "Name", "UpdatedAt" },
            values: new object[] { new Guid("11111111-1111-1111-1111-000000000001"), "#E67E22", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "🍔", true, "Comida", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

        migrationBuilder.InsertData(
            table: "Categories",
            columns: new[] { "Id", "Color", "CreatedAt", "Icon", "IsSystem", "Name", "UpdatedAt" },
            values: new object[] { new Guid("11111111-1111-1111-1111-000000000002"), "#3498DB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "🚗", true, "Transporte", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

        migrationBuilder.InsertData(
            table: "Categories",
            columns: new[] { "Id", "Color", "CreatedAt", "Icon", "IsSystem", "Name", "UpdatedAt" },
            values: new object[] { new Guid("11111111-1111-1111-1111-000000000003"), "#9B59B6", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "🏠", true, "Hogar", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

        migrationBuilder.InsertData(
            table: "Categories",
            columns: new[] { "Id", "Color", "CreatedAt", "Icon", "IsSystem", "Name", "UpdatedAt" },
            values: new object[] { new Guid("11111111-1111-1111-1111-000000000004"), "#1ABC9C", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "💊", true, "Salud", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

        migrationBuilder.InsertData(
            table: "Categories",
            columns: new[] { "Id", "Color", "CreatedAt", "Icon", "IsSystem", "Name", "UpdatedAt" },
            values: new object[] { new Guid("11111111-1111-1111-1111-000000000005"), "#E74C3C", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "🎮", true, "Ocio", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

        migrationBuilder.InsertData(
            table: "Categories",
            columns: new[] { "Id", "Color", "CreatedAt", "Icon", "IsSystem", "Name", "UpdatedAt" },
            values: new object[] { new Guid("11111111-1111-1111-1111-000000000006"), "#F39C12", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "📚", true, "Educación", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

        migrationBuilder.InsertData(
            table: "Categories",
            columns: new[] { "Id", "Color", "CreatedAt", "Icon", "IsSystem", "Name", "UpdatedAt" },
            values: new object[] { new Guid("11111111-1111-1111-1111-000000000007"), "#16A085", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "👕", true, "Ropa", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

        migrationBuilder.InsertData(
            table: "Categories",
            columns: new[] { "Id", "Color", "CreatedAt", "Icon", "IsSystem", "Name", "UpdatedAt" },
            values: new object[] { new Guid("11111111-1111-1111-1111-000000000008"), "#34495E", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "💡", true, "Servicios", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

        migrationBuilder.InsertData(
            table: "Categories",
            columns: new[] { "Id", "Color", "CreatedAt", "Icon", "IsSystem", "Name", "UpdatedAt" },
            values: new object[] { new Guid("11111111-1111-1111-1111-000000000009"), "#95A5A6", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Otros", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

        migrationBuilder.InsertData(
            table: "PaymentMethods",
            columns: new[] { "Id", "CreatedAt", "IsSystem", "Name", "UpdatedAt" },
            values: new object[] { new Guid("22222222-2222-2222-2222-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Efectivo", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

        migrationBuilder.InsertData(
            table: "PaymentMethods",
            columns: new[] { "Id", "CreatedAt", "IsSystem", "Name", "UpdatedAt" },
            values: new object[] { new Guid("22222222-2222-2222-2222-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Tarjeta de débito", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

        migrationBuilder.InsertData(
            table: "PaymentMethods",
            columns: new[] { "Id", "CreatedAt", "IsSystem", "Name", "UpdatedAt" },
            values: new object[] { new Guid("22222222-2222-2222-2222-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Tarjeta de crédito", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

        migrationBuilder.InsertData(
            table: "PaymentMethods",
            columns: new[] { "Id", "CreatedAt", "IsSystem", "Name", "UpdatedAt" },
            values: new object[] { new Guid("22222222-2222-2222-2222-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Transferencia", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

        migrationBuilder.InsertData(
            table: "PaymentMethods",
            columns: new[] { "Id", "CreatedAt", "IsSystem", "Name", "UpdatedAt" },
            values: new object[] { new Guid("22222222-2222-2222-2222-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Otro", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            table: "Categories",
            keyColumn: "Id",
            keyValue: new Guid("11111111-1111-1111-1111-000000000001"));

        migrationBuilder.DeleteData(
            table: "Categories",
            keyColumn: "Id",
            keyValue: new Guid("11111111-1111-1111-1111-000000000002"));

        migrationBuilder.DeleteData(
            table: "Categories",
            keyColumn: "Id",
            keyValue: new Guid("11111111-1111-1111-1111-000000000003"));

        migrationBuilder.DeleteData(
            table: "Categories",
            keyColumn: "Id",
            keyValue: new Guid("11111111-1111-1111-1111-000000000004"));

        migrationBuilder.DeleteData(
            table: "Categories",
            keyColumn: "Id",
            keyValue: new Guid("11111111-1111-1111-1111-000000000005"));

        migrationBuilder.DeleteData(
            table: "Categories",
            keyColumn: "Id",
            keyValue: new Guid("11111111-1111-1111-1111-000000000006"));

        migrationBuilder.DeleteData(
            table: "Categories",
            keyColumn: "Id",
            keyValue: new Guid("11111111-1111-1111-1111-000000000007"));

        migrationBuilder.DeleteData(
            table: "Categories",
            keyColumn: "Id",
            keyValue: new Guid("11111111-1111-1111-1111-000000000008"));

        migrationBuilder.DeleteData(
            table: "Categories",
            keyColumn: "Id",
            keyValue: new Guid("11111111-1111-1111-1111-000000000009"));

        migrationBuilder.DeleteData(
            table: "PaymentMethods",
            keyColumn: "Id",
            keyValue: new Guid("22222222-2222-2222-2222-000000000001"));

        migrationBuilder.DeleteData(
            table: "PaymentMethods",
            keyColumn: "Id",
            keyValue: new Guid("22222222-2222-2222-2222-000000000002"));

        migrationBuilder.DeleteData(
            table: "PaymentMethods",
            keyColumn: "Id",
            keyValue: new Guid("22222222-2222-2222-2222-000000000003"));

        migrationBuilder.DeleteData(
            table: "PaymentMethods",
            keyColumn: "Id",
            keyValue: new Guid("22222222-2222-2222-2222-000000000004"));

        migrationBuilder.DeleteData(
            table: "PaymentMethods",
            keyColumn: "Id",
            keyValue: new Guid("22222222-2222-2222-2222-000000000005"));
    }
}
