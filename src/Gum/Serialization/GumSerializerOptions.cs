using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gum.Serialization;

/// <summary>
/// Provides a json serializer that supports all the serializable types in Murder.
/// </summary>
public static class MurderSerializerOptionsExtensions
{
    /// <summary>
    /// Default options that should be used when serializing or deserializing any components
    /// within the project.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = GumSourceGenerationContext.Default
            .WithModifiers(SerializationModifiers.AddPrivateFieldsModifier),
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Replace,
        IncludeFields = true,
        WriteIndented = true,
        IgnoreReadOnlyFields = false,
        IgnoreReadOnlyProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };
}