using ControlDeFinanzasIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260912150000_AddRecurringExpenseProvenance")]
partial class AddRecurringExpenseProvenance
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // The runtime model is supplied by AppDbContext; this migration only
        // adds the two nullable provenance columns and their unique index.
    }
}
