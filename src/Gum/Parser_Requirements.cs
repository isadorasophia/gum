using Gum.InnerThoughts;
using Gum.Utilities;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Gum
{
    internal static partial class Tokens
    {
        public const string And = "and";
        public const string And2 = "&&";

        public const string Or = "or";
        public const string Or2 = "||";

        public const string Is = "is";

        public const string Less = "<";
        public const string LessEqual = "<=";
        public const string Equal = "==";
        public const string Bigger = ">";
        public const string BiggerEqual = ">=";

        public const string Different = "!=";

        public const string Else = "...";
    }

    public partial class Parser
    {
        /// <summary>
        /// Read the next criterion.
        /// </summary>
        /// <param name="line"></param>
        /// <param name="node">
        /// The resulting node. Only valid if it was able to read a non-empty valid
        /// requirement syntax.
        /// </param>
        /// <returns>Whether it read without any grammar errors.</returns>
        private bool ReadNextCriterion(ref ReadOnlySpan<char> line, int lineIndex, int currentColumn, out CriterionNode? node)
        {
            node = null;

            if (line.IsEmpty)
            {
                return true;
            }

            if (line.StartsWith(Tokens.Else))
            {
                // This is actually a "else" block!
                line = line.Slice(Tokens.Else.Length);
                currentColumn += Tokens.Else.Length;
            }

            // Check if we started specifying the relationship from the previous requirement.
            ReadOnlySpan<char> token = PopNextWord(ref line, out int end);
            if (end == -1)
            {
                // No requirement specified.
                return true;
            }

            // Diagnostics... 🙏
            currentColumn += token.Length;

            CriterionNodeKind? nodeKind = null;
            switch (token)
            {
                case Tokens.Or:
                case Tokens.Or2:
                    nodeKind = CriterionNodeKind.Or;
                    break;

                case Tokens.And:
                case Tokens.And2:
                    nodeKind = CriterionNodeKind.And;
                    break;

                    // TODO: Check for '...'
            }

            ReadOnlySpan<char> variableToken = token;
            if (nodeKind is not null)
            {
                variableToken = PopNextWord(ref line, out end);
                if (end == -1)
                {
                    // We saw something like a (and) condition. This is not really valid for us.
                    OutputHelpers.WriteError($"Unexpected condition end after '{token}' on line {lineIndex}.");
                    OutputHelpers.ProposeFixAtColumn(
                        lineIndex,
                        currentColumn,
                        arrowLength: 1,
                        content: _currentLine,
                        issue: "Unexpected end of condition.");

                    return false;
                }

                currentColumn += variableToken.Length;
            }
            else
            {
                // Assume 'AND' by default and move on.
                nodeKind = CriterionNodeKind.And;
            }

            // Check if this is actually a
            //      (!SomethingBad)
            //       ^
            // variable.
            CriterionKind criterionKind = CriterionKind.Is;
            if (variableToken[0] == (char)TokenChar.Negative)
            {
                criterionKind = CriterionKind.Different;
                variableToken = variableToken.Slice(1);
            }

            // 'token' now corresponds to the variable name. Check if there is a blackboard specified.
            (string? blackboard, string variable) = ReadBlackboardVariableName(variableToken);

            // The following are *optional*, so let's not kick off any characters yet.
            ReadOnlySpan<char> @operator = GetNextWord(line, out end);

            bool readNextToken = !@operator.IsEmpty;

            if (!@operator.IsEmpty)
            {
                switch (@operator)
                {
                    // is, ==
                    case Tokens.Is:
                    case Tokens.Equal:
                        break;

                    // !=
                    case Tokens.Different:
                        criterionKind = criterionKind == CriterionKind.Different ?
                            CriterionKind.Is : CriterionKind.Different;
                        break;

                    // <
                    case Tokens.Less:
                        criterionKind = CriterionKind.Less;
                        break;

                    // <=
                    case Tokens.LessEqual:
                        criterionKind = CriterionKind.LessOrEqual;
                        break;

                    // >=
                    case Tokens.BiggerEqual:
                        criterionKind = CriterionKind.BiggerOrEqual;
                        break;

                    // >
                    case Tokens.Bigger:
                        criterionKind = CriterionKind.Bigger;
                        break;

                    default:
                        // The next node will take care of this.
                        readNextToken = false;
                        break;
                }
            }

            FactKind? expectedFact;
            object? ruleValue;

            if (readNextToken)
            {
                line = line.Slice(end);
                currentColumn += end;

                // Check if we started specifying the relationship from the previous requirement.
                ReadOnlySpan<char> specifier = PopNextWord(ref line, out end);
                if (end == -1)
                {
                    // We have something like a '==' or 'is' waiting for another condition.
                    OutputHelpers.WriteError($"Unexpected condition end after '{@operator}' on line {lineIndex}.");
                    OutputHelpers.ProposeFixAtColumn(
                        lineIndex,
                        currentColumn,
                        arrowLength: 1,
                        content: _currentLine,
                        issue: "The matching value is missing.");

                    return false;
                }

                if (!ReadFactValue(specifier, out expectedFact, out ruleValue))
                {
                    OutputInvalidRuleValueSpecifier(specifier, lineIndex, currentColumn);
                    return false;
                }

                // currentColumn += end;
            }
            else
            {
                Debug.Assert(criterionKind == CriterionKind.Is || criterionKind == CriterionKind.Different,
                    "Unexpected criterion kind for a condition without an explicit token value!");

                // If there is no specifier, assume this is a boolean and the variable is enough.
                ruleValue = true;
                expectedFact = FactKind.Bool;
            }

            Fact fact = new(blackboard, variable, expectedFact.Value);
            Criterion criterion = new(fact, criterionKind, ruleValue);

            node = new(criterion, nodeKind.Value);

            return true;
        }

        private (string? Blackboard, string Variable) ReadBlackboardVariableName(ReadOnlySpan<char> value)
        {
            // 'token' now corresponds to the variable name. Check if there is a blackboard specified.
            string? blackboard;
            string variable;

            // First, check if there is a namespace specified.
            int blackboardIndex = value.IndexOf('.');
            if (blackboardIndex != -1)
            {
                blackboard = value.Slice(0, blackboardIndex).ToString();
                variable = value.Slice(blackboardIndex + 1).ToString();
            }
            else
            {
                blackboard = null;
                variable = value.ToString();
            }

            return (blackboard, variable);
        }

        private bool ReadFactValue(ReadOnlySpan<char> line, [NotNullWhen(true)] out FactKind? fact, [NotNullWhen(true)] out object? value)
        {
            fact = null;
            value = null;

            if (TryReadInteger(line) is int @int)
            {
                fact = FactKind.Int;
                value = @int;
            }
            else if (MemoryExtensions.Equals(line.Trim(), "true", StringComparison.OrdinalIgnoreCase))
            {
                fact = FactKind.Bool;
                value = true;
            }
            else if (MemoryExtensions.Equals(line.Trim(), "false", StringComparison.OrdinalIgnoreCase))
            {
                fact = FactKind.Bool;
                value = false;
            }
            else if ((line[0] == '\'' && line[line.Length - 1] == '\'') ||
                     (line[0] == '\"' && line[line.Length - 1] == '\"'))
            {
                fact = FactKind.String;
                value = line.Slice(1, line.Length - 2).ToString();
            }
            else
            {
                return false;
            }

            return true;
        }

        private void OutputInvalidRuleValueSpecifier(ReadOnlySpan<char> specifier, int lineIndex, int column)
        {
            // Unrecognized token?
            OutputHelpers.WriteError($"Unexpected rule value '{specifier}' on line {lineIndex}.");

            if (OutputTryGuessAssignmentValue(specifier, lineIndex))
            {
                return;
            }

            // We couldn't guess a fix. 😥 Sorry, just move on.
            OutputHelpers.ProposeFixAtColumn(
                lineIndex,
                column,
                arrowLength: specifier.Length,
                content: _currentLine,
                issue: "This rule could not be recognized.");
        }

        private bool OutputTryGuessAssignmentValue(ReadOnlySpan<char> specifier, int lineIndex)
        {
            if (char.IsDigit(specifier[0]))
            {
                char[] clean = Array.FindAll(specifier.ToArray(), char.IsDigit);
                OutputHelpers.ProposeFix(
                    lineIndex,
                    before: _currentLine,
                    after: _currentLine.Replace(specifier.ToString(), new string(clean)));

                return true;
            }

            string lowercaseSpecifier = specifier.ToString().ToLowerInvariant();

            int commonLength = lowercaseSpecifier.Intersect("true").Count();
            if (commonLength > 3)
            {
                OutputHelpers.ProposeFix(
                    lineIndex,
                    before: _currentLine,
                    after: _currentLine.Replace(specifier.ToString(), "true"));

                return true;
            }

            commonLength = lowercaseSpecifier.Intersect("false").Count();
            if (commonLength > 3)
            {
                OutputHelpers.ProposeFix(
                    lineIndex,
                    before: _currentLine,
                    after: _currentLine.Replace(specifier.ToString(), "false"));

                return true;
            }

            return false;
        }
    }
}