using System.Reflection;
using ControlDeFinanzasIA.Application.Abstractions.Llm;

namespace ControlDeFinanzasIA.Tests.Unit.Llm;

/// <summary>
/// Prueba de pureza por reflexión sobre <c>Application.Abstractions.Llm</c>
/// (F-013, slice 1, D-1). Es MÁS estricta que las reglas NetArchTest
/// existentes (<c>ArchitectureTests.cs:64-75</c>), que solo detectan el
/// prefijo <c>System.Net.Http</c>: esta prueba rechaza CUALQUIER tipo
/// <c>System.Net.*</c>, incluyendo <see cref="System.Net.HttpStatusCode"/>
/// (que vive en <c>System.Net.Primitives</c>, no en el paquete de
/// HttpClient, y es el tipo más natural para modelar "el proveedor
/// respondió 401" — la razón de ser de <see cref="LlmFailureKind"/>).
/// </summary>
/// <remarks>
/// Las referencias directas a los tipos del contrato en
/// <see cref="_contractTypes"/> son deliberadas: anclan este archivo a
/// esos tipos concretos para que, ANTES de que existan, el archivo falle
/// en COMPILAR en vez de pasar vacuamente sobre un namespace vacío. Ese
/// fallo de compilación es el estado RED de la tarea 1.1; la tarea 1.11
/// lo pone en verde al crear los tipos.
/// </remarks>
public sealed class LlmContractPurityTests
{
    private const string _llmContractNamespace = "ControlDeFinanzasIA.Application.Abstractions.Llm";
    private const string _forbiddenPrefix = "System.Net.";

    private static readonly Type[] _contractTypes =
    [
        typeof(LlmMessage),
        typeof(LlmRole),
        typeof(LlmRequest),
        typeof(LlmResponse),
        typeof(LlmException),
        typeof(LlmFailureKind),
        typeof(LlmConnectionOutcome),
        typeof(LlmConnectionStatus),
        typeof(LlmClientResolution),
        typeof(LlmConfigurationStatus),
        typeof(ILLMClient),
        typeof(ILLMClientFactory),
        typeof(IPromptProvider),
    ];

    [Fact]
    public void Contract_Anchor_List_Is_Not_Empty() =>
        // Guarda contra borrar accidentalmente la lista de anclaje de
        // arriba, lo que reintroduciría el riesgo de un pase vacuo.
        Assert.NotEmpty(_contractTypes);

    [Fact]
    public void No_Public_Type_In_Llm_Namespace_References_SystemNet()
    {
        Assembly applicationAssembly = typeof(LlmMessage).Assembly;

        var llmTypes = applicationAssembly
            .GetTypes()
            .Where(t => t.IsPublic && t.Namespace == _llmContractNamespace)
            .ToList();

        // Si esto falla, es la propia lista de anclaje la que quedó
        // desalineada con el namespace real — no un falso positivo del
        // resto de la prueba.
        Assert.NotEmpty(llmTypes);

        var offenders = new List<string>();

        foreach (Type type in llmTypes)
        {
            foreach (PropertyInfo property in type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                CollectOffenders(property.PropertyType, $"{type.FullName}.{property.Name} (property)", offenders);
            }

            foreach (ConstructorInfo ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (ParameterInfo parameter in ctor.GetParameters())
                {
                    CollectOffenders(parameter.ParameterType, $"{type.FullName}..ctor({parameter.Name})", offenders);
                }
            }

            foreach (MethodInfo method in type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName)
                {
                    // Getters/setters de propiedades ya se cubrieron arriba.
                    continue;
                }

                CollectOffenders(method.ReturnType, $"{type.FullName}.{method.Name} (return)", offenders);

                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    CollectOffenders(parameter.ParameterType, $"{type.FullName}.{method.Name}({parameter.Name})", offenders);
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "System.Net.* leak(s) found in Application.Abstractions.Llm:\n" + string.Join("\n", offenders));
    }

    private static void CollectOffenders(Type type, string memberDescription, List<string> offenders)
    {
        foreach (Type candidate in ExpandGenericArguments(type))
        {
            if (candidate.FullName?.StartsWith(_forbiddenPrefix, StringComparison.Ordinal) == true)
            {
                offenders.Add($"{memberDescription}: {candidate.FullName}");
            }
        }
    }

    /// <summary>
    /// Recorre un tipo y, recursivamente, sus argumentos genéricos y su
    /// tipo de elemento si es un arreglo — así <c>IReadOnlyList&lt;LlmMessage&gt;</c>
    /// o <c>Task&lt;LlmResponse&gt;</c> se inspeccionan en profundidad, no
    /// solo en su forma genérica externa.
    /// </summary>
    private static IEnumerable<Type> ExpandGenericArguments(Type type)
    {
        yield return type;

        if (type.IsGenericType)
        {
            foreach (Type argument in type.GetGenericArguments())
            {
                foreach (Type nested in ExpandGenericArguments(argument))
                {
                    yield return nested;
                }
            }
        }

        if (type.IsArray)
        {
            Type elementType = type.GetElementType()!;
            foreach (Type nested in ExpandGenericArguments(elementType))
            {
                yield return nested;
            }
        }
    }
}
