using System.Diagnostics;
using System.Text.RegularExpressions;
using Gum.InnerThoughts;
using Gum.Utilities;

namespace Gum
{
    /// <summary>
    /// These are the directives used to parse the current line instruction.
    /// </summary>
    internal enum TokenChar
    {
        None = 0,
        Situation = '=',
        BeginCondition = '(',
        EndCondition = ')',
        OnceBlock = '-',
        MultipleBlock = '+',
        BeginAction = '[',
        EndAction = ']',
        OptionBlock = '>',
        Flow = '@',
        Negative = '!',
        Debug = '%'
    }

    internal static partial class Tokens
    {
        public const string Comments = "//";
    }

    public partial class Parser
    {
        private static readonly Regex _indentation = new Regex(@"^(\t|    )*", RegexOptions.Compiled);
        private const char _separatorChar = ' ';

        private readonly string[] _lines;

        /// <summary>
        /// Each parser will consist into a single script.
        /// The owner shall be assigned once this gets instantiated in the engine.
        /// </summary>
        private readonly CharacterScript _script = new();

        /// <summary>
        /// The current block of dialog that currently belong to <see cref="CharacterScript.CurrentSituation"/>.
        /// </summary>
        private int _currentBlock = 0;

        private Block Block => _script.CurrentSituation.Blocks[_currentBlock];

        /// <summary>
        /// Current line without any comments, used for diagnostics.
        /// </summary>
        private string _currentLine = string.Empty;

        /// <summary>
        /// Keep tack of the latest index of each line.
        /// </summary>
        private int _lastIndentationIndex = 0;
        private int _indentationIndex = 0;

        /// <summary>
        /// The last directive '@random' to randomize the following choices.
        /// </summary>
        private bool _random = false;

        private bool ConsumeIsRandom()
        {
            bool random = _random;
            _random = false;

            return random;
        }

        /// <summary>
        /// The last directive '@' to play an amount of times.
        /// </summary>
        private int _playUntil = -1;

        private int ConsumePlayUntil()
        {
            int playUntil = _playUntil;
            _playUntil = -1;

            return playUntil;
        }

        //
        // Post-analysis variables.
        // 

        /// <summary>
        /// This is for validating all the goto destination statements.
        /// </summary>
        private readonly List<(Block Block, string Location, int Line)> _gotoDestinations = new();

        internal static CharacterScript? Parse(string file)
        {
            string[] lines = File.ReadAllLines(file);

            Parser parser = new(lines);
            return parser.Start();
        }

        internal Parser(string[] lines)
        {
            _lines = lines;
        }

        private int _indentBuffer = 0;

        internal CharacterScript? Start()
        {
            int index = 0;

            foreach (string rawLine in _lines)
            {
                index++;

                ReadOnlySpan<char> lineNoComments = rawLine.AsSpan();

                // First, start by ripping all the comments in this line.
                int comment = lineNoComments.IndexOf(Tokens.Comments);
                if (comment != -1)
                {
                    lineNoComments = lineNoComments.Slice(start: 0, length: comment);
                    lineNoComments = lineNoComments.TrimEnd();
                }

                ReadOnlySpan<char> lineNoIndent = lineNoComments.TrimStart();
                if (lineNoIndent.IsEmpty) continue;

                _currentLine = lineNoComments.ToString();

                // TODO: I think I can be fancy and use ReadOnlySpan here instead.
                // However, I couldn't really find a smart way to list the group matches with a ValueMatch yet.
                MatchCollection result = _indentation.Matches(_currentLine);

                // Count the indentation based on the regex captures result.
                _lastIndentationIndex = _indentationIndex;
                _indentationIndex = result[0].Groups[1].Captures.Count;

                // For science!
                int column = lineNoComments.Length - lineNoIndent.Length;

                if (lineNoIndent.IsEmpty) continue;

                if (_indentationIndex < _lastIndentationIndex)
                {
                    _indentBuffer += 1;
                }

                if (Defines(lineNoIndent, TokenChar.BeginCondition))
                {
                    _indentBuffer -= 1;
                }

                if (!ProcessLine(lineNoIndent, index, column))
                {
                    return null;
                }

                if (_script.HasCurrentSituation is false)
                {
                    OutputHelpers.WriteError($"Expected a situation (=) to be declared before line {index}.");
                    return null;
                }
            }

            return _script;
        }

        /// <summary>
        /// Check whether the first character of a line has a token defined.
        /// </summary>
        private bool Defines(ReadOnlySpan<char> line, TokenChar token, string? stringAfterToken = null)
        {
            ReadOnlySpan<char> word = GetNextWord(line, out int end).TrimStart();
            while (end != -1 && !word.IsEmpty)
            {
                if (word[0] == (char)token)
                {
                    if (stringAfterToken is null)
                    {
                        return true;
                    }
                    else if (word.Slice(1).StartsWith(stringAfterToken))
                    {
                        return true;
                    }
                }

                if (!Enum.IsDefined(typeof(TokenChar), (int)word[0]))
                {
                    return false;
                }

                if (end == line.Length)
                {
                    return false;
                }

                line = line.Slice(end);
                word = GetNextWord(line, out end).TrimStart();
            }

            return false;
        }

        /// <summary>
        /// Check whether the first character of a line has a token defined.
        /// </summary>
        private bool Defines(ReadOnlySpan<char> line, TokenChar[] tokens)
        {
            HashSet<char> tokensChar = tokens.Select(t => (char)t).ToHashSet();

            ReadOnlySpan<char> word = GetNextWord(line, out int end).TrimStart();
            while (end != -1 && !word.IsEmpty)
            {
                if (tokensChar.Contains(word[0]))
                {
                    return true;
                }

                if (Enum.IsDefined(typeof(TokenChar), (int)word[0]))
                {
                    return false;
                }

                if (end == line.Length)
                {
                    return false;
                }

                line = line.Slice(end);
            }

            return false;
        }

        /// <summary>
        /// Read the next line, without any comments.
        /// </summary>
        /// <returns>Whether it was successful and no error occurred.</returns>
        private bool ProcessLine(ReadOnlySpan<char> line, int index, int column, int depth = 0, bool hasCreatedJoinBlock = false)
        {
            if (line.IsEmpty) return true;

            bool isNestedBlock = false;

            // If this is not a situation declaration ('=') but a situation has not been declared yet!
            if (line[0] != (char)TokenChar.Situation && _script.HasCurrentSituation is false)
            {
                OutputHelpers.WriteError($"Expected a situation (=) to be declared before line {index}.");
                return false;
            }
            else if (depth == 0 && _script.HasCurrentSituation)
            {
                // We are on a valid situation, check whether we need to join dialogs.
                // Indentation changed:
                //     < from here
                // ^ to here
                if (_indentationIndex < _lastIndentationIndex)
                {
                    int totalToPop = 0;
                    bool createJoinBlock = true;
                    bool isChained = false;

                    if (Defines(line, TokenChar.BeginCondition))
                    {
                        createJoinBlock = false;

                        if (_indentBuffer <= 0 && !Defines(line, TokenChar.BeginCondition, Tokens.Else))
                        {
                            totalToPop = _lastIndentationIndex - _indentationIndex;
                            isChained = true;
                        }
                        else if (_lastIndentationIndex - _indentationIndex > 1)
                        {
                            totalToPop = 1;
                        }
                    }
                    else if (Defines(line, new TokenChar[] { 
                        TokenChar.Situation,
                        TokenChar.OptionBlock,
                        TokenChar.MultipleBlock, 
                        TokenChar.OnceBlock }))
                    {
                        totalToPop = 1;
                        createJoinBlock = false;
                        isChained = true;

                        if (line.Length > 1 && line[1] == (char)TokenChar.OptionBlock)
                        {
                            // Actually a ->
                            createJoinBlock = true;
                        }
                    }

                    while (totalToPop-- > 0)
                    {
                        _script.CurrentSituation.PopLastRelationship();
                    }

                    // Depending where we were, we may need to "join" different branches.
                    if (createJoinBlock)
                    {
                        _currentBlock = _script.CurrentSituation.AddBlock(ConsumePlayUntil(), join: true, isNested: false).Id;
                        hasCreatedJoinBlock = true;
                    }
                }
                else if (_indentationIndex > _lastIndentationIndex)
                {
                    // May be used if we end up creating a new block.
                    isNestedBlock = true;

                    if (Enum.IsDefined(typeof(TokenChar), (int)line[0]))
                    {
                        //switch ((TokenChar)line[0])
                        //{
                        //    case TokenChar.BeginAction:
                        //    case TokenChar.OnceBlock:
                        //        if (line.Length == 1 || line[1] != (char)TokenChar.OptionBlock)
                        //        {
                        //            // Actually a -
                        //            break;
                        //        }

                        //        // These won't create a block by default, so let's make sure that is the case when the indentation happens.
                        //        _currentBlock = _script.CurrentSituation.AddBlock(ConsumePlayUntil(), join: false, isNested: true).Id;
                        //        isNestedBlock = false;

                        //        break;
                        //}
                    }
                }
            }

            if (Enum.IsDefined(typeof(TokenChar), (int)line[0]))
            {
                TokenChar nextDirective = (TokenChar)line[0];

                // Eat this next token!
                line = line.Slice(1);
                column += 1;

                switch (nextDirective)
                {
                    // =
                    case TokenChar.Situation:
                        if (_indentationIndex >= 1)
                        {
                            OutputHelpers.WriteError($"We do not expect an indentation prior to a situation declaration on line {index}.");
                            OutputHelpers.ProposeFix(index, before: _currentLine, after: $"{TokenChar.Situation}{line}");

                            return false;
                        }

                        _script.AddNewSituation(line);
                        return true;

                    // @
                    case TokenChar.Flow:
                        ReadOnlySpan<char> command = GetNextWord(line, out int end);
                        if (command.Length == 0)
                        {
                            OutputHelpers.WriteError($"Empty flow (@) found on line {index}.");
                            return false;
                        }

                        // List of supported directives ('@'):
                        //  @random
                        //  @{number}
                        // ...that's all!
                        if (command.StartsWith("random"))
                        {
                            if (hasCreatedJoinBlock)
                            {
                                _ = _script.CurrentSituation.SwitchRelationshipTo(RelationshipKind.Random);
                            }
                            else
                            {
                                _random = true;
                            }
                        }
                        else if (TryReadInteger(command) is int number)
                        {
                            if (hasCreatedJoinBlock)
                            {
                                _ = Block.PlayUntil = number;
                            }
                            else
                            {
                                _playUntil = number;
                            }
                        }
                        else
                        {
                            // Failed reading the command :(
                            TryGuessFlowDirectiveError(command, index);
                            return false;
                        }

                        if (end == -1)
                        {
                            return true;
                        }
                        else
                        {
                            column += end;
                            line = line.Slice(end).TrimStart();
                        }

                        break;

                    // (
                    case TokenChar.BeginCondition:
                        // Check for the end of the condition block ')'
                        int endColumn = MemoryExtensions.IndexOf(line, (char)TokenChar.EndCondition);
                        if (endColumn == -1)
                        {
                            OutputHelpers.WriteError($"Missing matching '{(char)TokenChar.EndCondition}' on line {index}.");
                            OutputHelpers.ProposeFix(
                                index, 
                                before: _currentLine, 
                                after: _currentLine.TrimEnd() + (char)TokenChar.EndCondition);

                            return false;
                        }

                        // Create the condition block.

                        line = line.Slice(0, endColumn);
                        if (!hasCreatedJoinBlock)
                        {
                            RelationshipKind relationshipKind = RelationshipKind.Next;
                            if (line.StartsWith(Tokens.Else))
                            {
                                relationshipKind = RelationshipKind.IfElse;
                            }

                            _currentBlock = _script.CurrentSituation.AddBlock(
                                ConsumePlayUntil(), join: false, isNestedBlock, relationshipKind).Id;
                        }

                        return ParseConditions(line, index, column);

                    // [
                    case TokenChar.BeginAction:
                        // Check for the end of the condition block ']'
                        int endAction = MemoryExtensions.IndexOf(line, (char)TokenChar.EndAction);
                        if (endAction == -1)
                        {
                            OutputHelpers.WriteError($"Missing matching '{(char)TokenChar.EndAction}' on line {index}.");
                            OutputHelpers.ProposeFix(
                                index,
                                before: _currentLine,
                                after: _currentLine.TrimEnd() + (char)TokenChar.EndAction);

                            return false;
                        }

                        line = line.Slice(0, endAction);
                        return ParseAction(line, index, column);

                    // -
                    case TokenChar.OnceBlock:
                        // Check whether this is actually a '->'
                        if (!line.IsEmpty && line[0] == (char)TokenChar.OptionBlock)
                        {
                            line = line.Slice(1);
                            column += 1;

                            return ParseGoto(line, index, column);
                        }

                        _playUntil = 1;
                        return ParseOption(line, index, column, join: false, isNestedBlock);

                    // +
                    case TokenChar.MultipleBlock:
                        _playUntil = -1;
                        return ParseOption(line, index, column, join: false, isNestedBlock);

                    // >
                    case TokenChar.OptionBlock:
                        return ParseChoice(line, index, column, join: false, isNestedBlock);

                    default:
                        return true;
                }
            }
            else
            {
                return ParseLine(line, index, column);
            }

            if (!line.IsEmpty)
            {
                return ProcessLine(line, index, column, depth + 1, hasCreatedJoinBlock);
            }

            return true;
        }

        private bool ParseChoice(ReadOnlySpan<char> line, int lineIndex, int columnIndex, bool join, bool nested)
        {
            _currentBlock = _script.CurrentSituation.AddBlock(
                    ConsumePlayUntil(), join, nested, RelationshipKind.Choice).Id;

            line = line.TrimStart().TrimEnd();
            Block.AddLine(line);

            return true;
        }

        private bool ParseOption(ReadOnlySpan<char> line, int lineIndex, int columnIndex, bool join, bool nested)
        {
            RelationshipKind relationshipKind = RelationshipKind.HighestScore;
            if (ConsumeIsRandom())
            {
                relationshipKind = RelationshipKind.Random;
            }

            _currentBlock = _script.CurrentSituation.AddBlock(
                    ConsumePlayUntil(), join, nested, relationshipKind).Id;

            line = line.TrimStart().TrimEnd();
            Block.AddLine(line);

            return true;
        }

        private bool ParseLine(ReadOnlySpan<char> line, int _, int __)
        {
            if (_script.CurrentSituation.Blocks.Count == 0)
            {                      
                _currentBlock = _script.CurrentSituation.AddBlock(
                    ConsumePlayUntil(), join: false, isNested: false, RelationshipKind.Next).Id;
            }

            // This is probably just a line! So let's just read as it is.
            // TODO: Check for speaker.
            Block.AddLine(line);
            return true;
        }

        private bool ParseGoto(ReadOnlySpan<char> line, int lineIndex, int currentColumn)
        {
            // Check if we started specifying the relationship from the previous requirement.
            ReadOnlySpan<char> location = line.Trim();
            if (location.IsEmpty)
            {
                // We saw something like a (and) condition. This is not really valid for us.
                OutputHelpers.WriteError($"Expected a situation after '->'.");
                OutputHelpers.ProposeFixAtColumn(
                    lineIndex,
                    currentColumn,
                    arrowLength: 1,
                    content: _currentLine,
                    issue: "Did you forget a destination here?");

                return false;
            }

            bool isExit = false;
            if (MemoryExtensions.Equals(location, "exit!", StringComparison.OrdinalIgnoreCase))
            {
                // If this is an 'exit!' keyword, finalize right away.
                isExit = true;
            }
            else
            {
                // Otherwise, keep track of this and add at the end.
                _gotoDestinations.Add((Block, location.ToString(), lineIndex));
            }

            _script.CurrentSituation.MarkGotoOnBlock(_currentBlock, isExit);

            return true;
        }

        /// <summary>
        /// This reads and parses a condition into <see cref="_currentBlock"/>.
        /// Expected format is:
        ///     (HasSomething is true)
        ///      ^ begin of span    ^ end of span
        /// 
        /// </summary>
        /// <returns>Whether it succeeded parsing the line.</returns>
        private bool ParseConditions(ReadOnlySpan<char> line, int lineIndex, int currentColumn)
        {
            while (true)
            {
                ReadOnlySpan<char> previousLine = line;
                if (!ReadNextCriterion(ref line, lineIndex, currentColumn, out CriterionNode? node))
                {
                    return false;
                }

                currentColumn += previousLine.Length - line.Length;

                if (node is null)
                {
                    return true;
                }

                Block.AddRequirement(node.Value);
            }
        }

        /// <summary>
        /// Fetches the immediate next word of a line.
        /// This disregards any indentation or white space prior to the word.
        /// </summary>
        /// <param name="end">The end of the parameter. If -1, this is an empty word.</param>
        private static ReadOnlySpan<char> GetNextWord(ReadOnlySpan<char> line, out int end)
        {
            ReadOnlySpan<char> trimmed = line.TrimStart();
            int separatorIndex = trimmed.IndexOf(_separatorChar);

            ReadOnlySpan<char> result = separatorIndex == -1 ?
                trimmed : trimmed.Slice(0, separatorIndex);

            end = trimmed.IsEmpty ? -1 : result.Length + (line.Length - trimmed.Length);

            return result;
        }

        /// <summary>
        /// Fetches and removes the next word of <paramref name="line"/>.
        /// This disregards any indentation or white space prior to the word.
        /// </summary>
        /// <param name="end">The end of the parameter. If -1, this is an empty word.</param>
        private static ReadOnlySpan<char> PopNextWord(ref ReadOnlySpan<char> line, out int end)
        {
            ReadOnlySpan<char> result = GetNextWord(line, out end);
            if (end != -1)
            {
                line = line.Slice(end);
            }

            return result;
        }

        /// <summary>
        /// Expects to read an integer of a line such as:
        ///     "28 (Something else)"   -> valid
        ///     "28something"           -> invalid
        ///     "28"                    -> valid
        /// </summary>
        private int? TryReadInteger(ReadOnlySpan<char> maybeInteger)
        {
            if (int.TryParse(maybeInteger, out int result))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Try to guess why we failed parsing a '@' directive.
        /// </summary>
        private void TryGuessFlowDirectiveError(ReadOnlySpan<char> directive, int index)
        {
            OutputHelpers.WriteError($"Unable to recognize '@{directive}' directive on line {index}.");

            if (char.IsDigit(directive[0]))
            {
                char[] clean = Array.FindAll(directive.ToArray(), char.IsDigit);
                OutputHelpers.ProposeFix(
                    index, 
                    before: _currentLine, 
                    after: _currentLine.Replace(directive.ToString(), new string(clean)));

                return;
            }

            int commonLength = directive.ToArray().Intersect("random").Count();
            if (commonLength > 3)
            {
                OutputHelpers.ProposeFix(
                    index, 
                    before: _currentLine, 
                    after: _currentLine.Replace(directive.ToString(), "random"));

                return;
            }

            OutputHelpers.Remark("We currently support '@{number}' and '@random' as valid directives. Please, reach out if this was not clear. 🙏");
        }
    }
}
