using Gum.Blackboards;
using Gum.Utilities;
using System.Diagnostics;
using System.Text;

namespace Gum.InnerThoughts
{
    [DebuggerDisplay("{DebuggerDisplay(),nq}")]
    public class DialogAction
    {
        public readonly Fact Fact = new();

        public readonly BlackboardActionKind Kind = BlackboardActionKind.Set;

        public readonly string? StrValue = null;

        public readonly int? IntValue = null;

        public readonly bool? BoolValue = null;

        public readonly string? ComponentValue = null;

        public readonly float? FloatValue = null;

        public DialogAction() { }

        public DialogAction(Fact fact, BlackboardActionKind kind, object value)
        {
            bool? @bool = null;
            int? @int = null;
            string? @string = null;
            float? @float = null;

            string? component = null;

            // Do not propagate previous values.
            switch (fact.Kind)
            {
                case FactKind.Bool:
                    @bool = (bool)value;
                    break;

                case FactKind.Int:
                    @int = (int)value;
                    break;

                case FactKind.String:
                    @string = (string)value;
                    break;

                case FactKind.Component:
                    component = (string)value;
                    break;

                case FactKind.Float:
                    @float = (float)@value;
                    break;
            }

            (Fact, Kind, StrValue, IntValue, BoolValue, ComponentValue, FloatValue) = (fact, kind, @string, @int, @bool, component, @float);
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

                case FactKind.Component:
                    result = result.Append(ComponentValue);
                    break;

                case FactKind.Float:
                    result = result.Append(FloatValue);
                    break;
            }

            result = result.Append(']');

            return result.ToString();
        }
    }
}