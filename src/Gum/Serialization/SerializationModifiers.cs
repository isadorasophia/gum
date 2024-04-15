﻿using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Gum.Serialization;

internal static class SerializationModifiers
{
    public static IJsonTypeInfoResolver WithModifiers(this IJsonTypeInfoResolver resolver, params Action<JsonTypeInfo>[] modifiers)
        => new ModifierResolver(resolver, modifiers);

    private sealed class ModifierResolver : IJsonTypeInfoResolver
    {
        private readonly IJsonTypeInfoResolver _source;
        private readonly Action<JsonTypeInfo>[] _modifiers;

        public ModifierResolver(IJsonTypeInfoResolver source, Action<JsonTypeInfo>[] modifiers)
        {
            _source = source;
            _modifiers = modifiers;
        }

        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            JsonTypeInfo? typeInfo = _source.GetTypeInfo(type, options);
            if (typeInfo != null)
            {
                foreach (Action<JsonTypeInfo> modifier in _modifiers)
                {
                    modifier(typeInfo);
                }
            }

            return typeInfo;
        }
    }

    private static readonly Dictionary<Type, List<JsonPropertyInfo>?> _types = [];

    public static void AddPrivateFieldsModifier(JsonTypeInfo jsonTypeInfo)
    {
        if (jsonTypeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        Type? t = jsonTypeInfo.Type;
        if (t.Assembly.FullName is null || t.Assembly.FullName.StartsWith("System"))
        {
            // Ignore system types.
            return;
        }

        HashSet<string> existingProperties = new(jsonTypeInfo.Properties.Count, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < jsonTypeInfo.Properties.Count; ++i)
        {
            JsonPropertyInfo property = jsonTypeInfo.Properties[i];

            bool shouldRemoveProperty = ShouldRemoveProperty(property, t);
            if (shouldRemoveProperty)
            {
                jsonTypeInfo.Properties.RemoveAt(i);
                --i;
            }
            else
            {
                existingProperties.Add(property.Name);
            }
        }

        while (t is not null && t.Assembly.FullName is string name && !name.StartsWith("System"))
        {
            if (_types.TryGetValue(t, out var extraFieldsInParentType))
            {
                if (extraFieldsInParentType is not null)
                {
                    foreach (JsonPropertyInfo info in extraFieldsInParentType)
                    {
                        if (!existingProperties.Contains(info.Name))
                        {
                            JsonPropertyInfo infoForThisType = jsonTypeInfo.CreateJsonPropertyInfo(info.PropertyType, info.Name);

                            infoForThisType.Get = info.Get;
                            infoForThisType.Set = info.Set;

                            jsonTypeInfo.Properties.Add(infoForThisType);

                            existingProperties.Add(infoForThisType.Name);
                        }
                    }
                }
            }
            else
            {
                bool fetchedConstructors = false;
                HashSet<string>? parameters = null;

                // Slightly evil in progress code. If the field is *not* found as any of the constructor parameters,
                // manually use the setter via reflection. I don't care.
                for (int i = 0; i < jsonTypeInfo.Properties.Count; i++)
                {
                    JsonPropertyInfo info = jsonTypeInfo.Properties[i];

                    if (info.Set is not null)
                    {
                        continue;
                    }

                    if (!fetchedConstructors)
                    {
                        parameters = FetchConstructorParameters(t);
                    }

                    if (parameters is null || !parameters.Contains(info.Name))
                    {
                        FieldInfo? field = t.GetField(info.Name);
                        if (field is not null)
                        {
                            info.Set = field.SetValue;
                        }
                    }
                }

                List<JsonPropertyInfo>? extraPrivateProperties = null;

                // Now, this is okay. There is not much to do here. If the field is private, manually fallback to reflection.
                foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (!Attribute.IsDefined(field, typeof(JsonIncludeAttribute)) || !Attribute.IsDefined(field, typeof(SerializeAttribute)))
                    {
                        continue;
                    }

                    string fieldName = field.Name;

                    // We may need to manually format names for private fields.
                    if (fieldName.StartsWith('_'))
                    {
                        fieldName = fieldName[1..];
                    }

                    if (existingProperties.Contains(fieldName))
                    {
                        continue;
                    }

                    JsonPropertyInfo jsonPropertyInfo = jsonTypeInfo.CreateJsonPropertyInfo(field.FieldType, fieldName);

                    jsonPropertyInfo.Get = field.GetValue;
                    jsonPropertyInfo.Set = field.SetValue;

                    jsonTypeInfo.Properties.Add(jsonPropertyInfo);

                    extraPrivateProperties ??= [];
                    extraPrivateProperties.Add(jsonPropertyInfo);

                    existingProperties.Add(jsonPropertyInfo.Name);
                }

                _types[t] = extraPrivateProperties;
            }

            t = t.BaseType;
        }
    }

    private static bool ShouldRemoveProperty(JsonPropertyInfo property, Type t)
    {
        if (property.ShouldSerialize is not null)
        {
            // This means that are already rules in place that will likely deal with this serialization.
            return false;
        }

        if (property.Set is not null)
        {
            // Setter is available! Don't bother.
            return false;
        }

        if (t.GetProperty(property.Name) is not PropertyInfo prop)
        {
            // Fields are okay!
            return false;
        }

        if (prop.SetMethod is not null)
        {
            property.Set = prop.SetValue;
            return false;
        }

        // Skip readonly properties. Apparently System.Text.Json likes to ignore ReadOnlyProperties=false when applying to collections
        // so we will manually ignore them here.
        // These won't have a setter and that's why we reached this point.
        return true;
    }

    private static HashSet<string>? FetchConstructorParameters([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type t)
    {
        Type tAttribute = typeof(JsonConstructorAttribute);
        ConstructorInfo[] constructors = t.GetConstructors();

        foreach (ConstructorInfo info in constructors)
        {
            if (!Attribute.IsDefined(info, tAttribute))
            {
                continue;
            }

            HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);

            foreach (ParameterInfo parameter in info.GetParameters())
            {
                if (parameter.Name is null)
                {
                    continue;
                }

                result.Add(parameter.Name);
            }

            return result;
        }

        return null;
    }
}
