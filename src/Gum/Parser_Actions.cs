using Gum.Blackboards;
using Gum.InnerThoughts;
using Gum.Utilities;

namespace Gum
{
    internal static partial class Tokens
    {
        public const char Assign = '=';
        public const string Add = "+=";
        public const string Minus = "-=";

        public const string Component = "c:";
    }

    public partial class Parser
    {
        /// <summary>
        /// This reads and parses an action into <see cref="_currentBlock"/>.
        /// Expected format is:
        ///     [SomeVariable = true]
        ///      ^ begin of span   ^ end of span
        /// 
        /// </summary>
        /// <returns>Whether it succeeded parsing the action.</returns>
        private bool ParseAction(ReadOnlySpan<char> line, int lineIndex, int currentColumn)
        {
            if (line.IsEmpty)
            {
                // We saw something like a (and) condition. This is not really valid for us.
                OutputHelpers.WriteWarning($"Empty action ('[]') found at line {lineIndex}. " +
                    "Was this on purpose? Because it will be ignored.");
                return true;
            }

            CheckAndCreateLinearBlock(joinLevel: 0, isNested: false);

            if (line.StartsWith(Tokens.Component))
            {
                line = line.Slice(Tokens.Component.Length);

                string component = line.ToString();

                Block.AddAction(
                    new DialogAction(
                        new Fact(componentType: component), 
                        BlackboardActionKind.Component, component));
                return true;
            }

            BlackboardActionKind? actionKind = null;

            int index = line.IndexOf(Tokens.Minus);
            if (index != -1)
            {
                actionKind = BlackboardActionKind.Minus;
            }

            if (actionKind is null &&
                (index = line.IndexOf(Tokens.Add)) != -1)
            {
                actionKind = BlackboardActionKind.Add;
            }

            if (actionKind is null && 
                (index = line.IndexOf(Tokens.Assign)) != -1)
            {
                actionKind = BlackboardActionKind.Set;
            }

            if (actionKind is null)
            {
                OutputHelpers.WriteError($"Unable to find an assignment for action '[{line}]' on line {lineIndex}.");

                if (line.ToString().Contains("Interaction"))
                {
                    OutputHelpers.ProposeFix(
                        lineIndex,
                        before: _currentLine,
                        after: _currentLine.Replace(line.ToString(), $"c:{line}"));

                    return false;
                }

                OutputHelpers.ProposeFixAtColumn(
                    lineIndex,
                    currentColumn,
                    arrowLength: line.Length,
                    content: _currentLine,
                    issue: $"Expected a component ({Tokens.Component}) or an assignment " +
                        $"({Tokens.Minus}, {Tokens.Add}, {Tokens.Assign}).");

                return false;
            }

            // Read the first half.

            ReadOnlySpan<char> variableName = line.Slice(0, index).TrimEnd();
            (string? blackboard, string variable) = ReadBlackboardVariableName(variableName);

            line = line.Slice(index);
            currentColumn += index;

            FactKind? factKind = null;
            object? value = null;

            if (actionKind == BlackboardActionKind.Minus || 
                actionKind == BlackboardActionKind.Add)
            {
                // += or -=
                line = line.Slice(Tokens.Minus.Length); // minus and add has the same length, so i will keep this simple...
                currentColumn += Tokens.Minus.Length;

                factKind = FactKind.Int;
                value = TryReadInteger(line);
                if (value is not int)
                {
                    OutputHelpers.WriteError($"Expected '{line}' to be an integer on line {lineIndex}.");

                    char[] clean = Array.FindAll(line.ToArray(), char.IsDigit);
                    if (clean.Length != 0)
                    {
                        OutputHelpers.ProposeFix(
                            lineIndex,
                            before: _currentLine,
                            after: _currentLine.Replace(line.ToString(), new string(clean)));

                        return false;
                    }

                    // We couldn't guess a fix. 😥 Sorry, just move on.
                    OutputHelpers.ProposeFixAtColumn(
                        lineIndex,
                        currentColumn,
                        arrowLength: line.Length,
                        content: _currentLine,
                        issue: "Expected an integer value.");
                    return false;
                }
            }
            else
            {
                // =
                line = line.Slice(1); // minus and add has the same length, so i will keep this simple...
                currentColumn += 1;

                if (line.IsEmpty)
                {
                    OutputHelpers.WriteError($"Empty assignment for '[{variableName}=]' on line {lineIndex}.");

                    OutputHelpers.ProposeFixAtColumn(
                        lineIndex,
                        currentColumn,
                        arrowLength: 1,
                        content: _currentLine,
                        issue: "Expected a boolean, integer or string value.");

                    return false;
                }

                if (!ReadFactValue(line, out factKind, out value))
                {
                    OutputHelpers.WriteError($"Expected '{line}' to be an integer, boolean or string on line {lineIndex}.");

                    if (OutputTryGuessAssignmentValue(line, lineIndex, currentColumn))
                    {
                        return false;
                    }

                    OutputHelpers.ProposeFixAtColumn(
                        lineIndex,
                        currentColumn,
                        arrowLength: line.Length,
                        content: _currentLine,
                        issue: "Expected a boolean, integer or string value.");

                    return false;
                }
            }

            Fact fact = new(blackboard, variable, factKind.Value);
            DialogAction action = new(fact, actionKind.Value, value);

            Block.AddAction(action);

            return true;
        }
    }
}
