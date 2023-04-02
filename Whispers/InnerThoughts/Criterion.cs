using System.Text;
using System.Diagnostics;

namespace Whispers.InnerThoughts
{
    [DebuggerDisplay("{DebuggerDisplay(),nq}")]
    public readonly struct Criterion
    {
        public readonly Fact Fact = new();

        public readonly CriterionKind Kind = CriterionKind.Is;

        public readonly string? StrValue = null;

        public readonly int? IntValue = null;

        public readonly bool? BoolValue = null;

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
            }

            (Fact, Kind, StrValue, IntValue, BoolValue) = (fact, kind, @string, @int, @bool);
        }

        public string DebuggerDisplay()
        {
            StringBuilder result = new();

            result = result.Append($"{Fact.Name}: {Kind}");

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
            }

            return result.ToString();
        }
    }
}
