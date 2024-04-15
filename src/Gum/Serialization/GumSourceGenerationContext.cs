using Gum.InnerThoughts;
using System.Text.Json.Serialization;

namespace Gum.Serialization;

[JsonSerializable(typeof(CharacterScript))]
[JsonSerializable(typeof(Situation))]
internal partial class GumSourceGenerationContext : JsonSerializerContext
{ }