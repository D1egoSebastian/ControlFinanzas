namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Abstracción de "seam" para acceder a las rutas de archivos de
/// datos sin acoplarse a <see cref="System.Environment"/>.
/// Toda la aplicación (repositories que abren el archivo de base
/// de datos, código que escribe logs, etc.) recibe
/// <see cref="IPathResolver"/> por inyección de dependencias.
/// </summary>
/// <remarks>
/// Las implementaciones reales (<c>SystemPathResolver</c> en F-008)
/// resuelven las rutas por sistema operativo según
/// <c>tech-stack.md</c>. Esta interfaz es agnóstica al SO.
/// </remarks>
public interface IPathResolver
{
    /// <summary>
    /// Retorna la ruta **del directorio** donde la aplicación
    /// persiste datos. No incluye el nombre del archivo.
    /// </summary>
    /// <remarks>
    /// La implementación real devuelve una ruta que termina (o no)
    /// con separador según la convención del SO. La interfaz
    /// no garantiza formato; los callers deben usar
    /// <see cref="System.IO.Path.Combine(string, string)"/> para
    /// construir rutas completas.
    /// </remarks>
    string DataDirectory { get; }

    /// <summary>
    /// Retorna la ruta **completa** al archivo de base de datos,
    /// incluyendo el directorio y el nombre del archivo.
    /// </summary>
    /// <remarks>
    /// Derivable de <see cref="DataDirectory"/> + nombre del
    /// archivo, pero se expone para que los callers no tengan que
    /// conocer la convención del nombre (p. ej.
    /// <c>app.db</c>, <c>data.sqlite</c>, etc.).
    /// </remarks>
    string DatabaseFilePath { get; }
}
