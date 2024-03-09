using Gum.Utilities;
using System.Diagnostics;
using System.Text;

namespace Gum.InnerThoughts
{
    [DebuggerDisplay("{DebuggerDisplay(),nq}")]
    public readonly struct Criterion
    {
        public readonly Fact Fact = new();

        public readonly CriterionKind Kind = CriterionKind.Is;

        public readonly string? StrValue = null;

        public readonly int? IntValue = null;

        public readonly bool? BoolValue = null;

        public readonly float? FloatValue = null;

        public Criterion() { }

        /// <summary>
        /// Creates a fact of type <see cref="FactKind.Weight"/>.
        /// </summary>
        public static Criterion Weight => new(Fact.Weight, CriterionKind.Is, 1);

        /// <summary>
        /// Creates a fact of type <see cref="FactKind.Component"/>.
        /// </summary>
        public static Criterion Component => new(Fact.Component, CriterionKind.Is, true);

        public Criterion(Fact fact, CriterionKind kind, object @value)
        {
            bool? @bool = null;
            int? @int = null;
            string? @string = null;
            float? @float = null;

            // Do not propagate previous values.
            switch (fact.Kind)
            {
                case FactKind.Bool:
                    @bool = (bool)@value;
                    break;

                case FactKind.Int:
                    @int = (int)@value;
                    break;

                case FactKind.String:
                    @string = (string)@value;
                    break;

                case FactKind.Weight:
                    @int = (int)@value;
                    break;

                case FactKind.Float:
                    @float = (float)@value;
                    break;
            }

            (Fact, Kind, StrValue, IntValue, BoolValue, FloatValue) = (fact, kind, @string, @int, @bool, @float);
        }

        public string DebuggerDisplay()
        {
            StringBuilder result = new();

            if (Fact.Kind == FactKind.Component)
            {
                result = result.Append($"[c:");
            }
            else
            {
                result = result.Append($"[{Fact.Name} {OutputHelpers.ToCustomString(Kind)} ");
            }

            switch (Fact.Kind)
            {
                case FactKind.Bool:
                    result = result.Append(BoolValue);
                    break;

                case FactKind.Int:
                    result = result.Append(IntValue);
                    break;

                case FactKind.String:
                    result = result.Append(StrValue);
                    break;

                case FactKind.Weight:
                    result = result.Append(IntValue);
                    break;

                case FactKind.Float:
                    result = result.Append(FloatValue);
                    break;
            }

            _ = result.Append(']');

            return result.ToString();
        }
    }
}