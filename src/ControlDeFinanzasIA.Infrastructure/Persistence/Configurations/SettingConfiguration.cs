using ControlDeFinanzasIA.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuracion EF Core para <see cref="Setting"/>. El indice unico en
/// <see cref="Setting.Key"/> es lo que hace seguro el upsert-por-clave de
/// <c>SettingsStore.SetAsync</c> (F-012 D-6).
/// </summary>
public sealed class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.ToTable("Settings");
        builder.HasKey(setting => setting.Id);
        builder.Property(setting => setting.Id).ValueGeneratedNever();
        builder.Property(setting => setting.Key).IsRequired().HasMaxLength(Setting.MaxKeyLength);
        builder.Property(setting => setting.Value).IsRequired().HasMaxLength(Setting.MaxValueLength);

        // El upsert por clave de SettingsStore.SetAsync depende de esto a nivel de
        // esquema: sin el indice unico, un SetAsync concurrente insertaria un
        // duplicado silencioso en vez de fallar (proposal R-7).
        builder.HasIndex(setting => setting.Key).IsUnique();
    }
}
