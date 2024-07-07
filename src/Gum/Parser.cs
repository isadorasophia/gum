using Gum.InnerThoughts;
using Gum.Utilities;
using System.Text.RegularExpressions;

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
        ChoiceBlock = '>',
        Flow = '@',
        Negative = '!',
        Chance = '%'
    }

    internal static partial class Tokens
    {
        public const string Comments = "//";
    }

    public partial class Parser
    {
        private static readonly Regex _indentation = new Regex(@"^(\t|    |[-+]   )*", RegexOptions.Compiled);
        private const char _separatorChar = ' ';

        private readonly string[] _lines;

        /// <summary>
        /// Each parser will consist into a single script.
        /// The owner shall be assigned once this gets instantiated in the engine.
        /// </summary>
        private readonly CharacterScript _script;

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

        /// <summary>
        /// If applicable, tracks the first token of the last line.
        /// This is used to tweak our own indentation rules.
        /// </summary>
        private TokenChar? _lastLineToken = null;

        private int _indentationIndex = 0;

        /// <summary>
        /// The last directive '@random' to randomize the following choices.
        /// </summary>
        private bool _random = false;

        /// <summary>
        /// Whether the last line processed was an action.
        /// </summary>
        private bool _wasPreviousAction = false;

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

        /// <summary>
        /// The last directive '%' to dialogue chance.
        /// </summary>
        private float _chance = 1;

        private int ConsumePlayUntil()
        {
            int playUntil = _playUntil;
            _playUntil = -1;

            return playUntil;
        }

        private float ConsumeChance()
        {
            float chance = _chance;
            _chance = 1;

            return chance;
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

            Parser parser = new(name: Path.GetFileNameWithoutExtension(file), lines);
            return parser.Start();
        }

        internal Parser(string name, string[] lines)
        {
            _script = new(name);
            _lines = lines;
        }

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

                if (!ProcessLine(lineNoIndent, index, column))
                {
                    return null;
                }

                // Track whatever was the last token used.
                if (Enum.IsDefined(typeof(TokenChar), (int)lineNoIndent[0]))
                {
                    _lastLineToken = (TokenChar)lineNoIndent[0];
                }
                else
                {
                    _lastLineToken = null;
                }

                if (_script.HasCurrentSituation is false)
                {
                    OutputHelpers.WriteError($"Expected a situation (=) to be declared before line {index}.");
                    return null;
                }
            }

            if (!ResolveAllGoto())
            {
                return null;
            }

            _ = Trim();

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

                if (end >= line.Length)
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
        private bool ProcessLine(ReadOnlySpan<char> line, int index, int column, int depth = 0, int joinLevel = 0, bool hasCreatedBlock = false)
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
                // This fixes the lack of indentation of choice dialogs. This is so we can
                // properly join scenarios such as:
                //
                //  >> Something...
                //  > Or else!
                //  > No?
                //
                //  Okay.
                bool isChoice = Defines(line, TokenChar.ChoiceBlock);
                if (_indentationIndex == _lastIndentationIndex &&
                    _lastLineToken == TokenChar.ChoiceBlock && !isChoice)
                {
                    _lastIndentationIndex += 1;
                }

                // We are on a valid situation, check whether we need to join dialogs.
                // Indentation changed:
                //     < from here
                // ^ to here
                if (_indentationIndex < _lastIndentationIndex)
                {
                    joinLevel = _lastIndentationIndex - _indentationIndex;
                    bool createJoinBlock = true;

                    // If the last line was actually a flow (@) and this is a join,
                    // we'll likely disregard the last join.
                    //
                    //  @1  Hi!
                    //      Bye.
                    //
                    //  Join. <- this will apply a join.
                    //
                    //  @1  Hi!
                    //  Bye. <- this will apply a join.
                    //
                    //  (something)
                    //      @1  Hi!
                    //
                    //  Bye. <- this will apply a join on (something).
                    if (_lastLineToken == TokenChar.Flow &&
                        _script.CurrentSituation.PeekLastBlockParent().Conditional)
                    {
                        joinLevel += 1;
                    }

                    if (Defines(line, TokenChar.BeginCondition))
                    {
                        createJoinBlock = false;

                        Block lastBlock = _script.CurrentSituation.PeekLastBlock();
                        Block parent = _script.CurrentSituation.PeekBlockAt(joinLevel);

                        // This might backfire when it's actually deeper into the condition, but okay.
                        if (lastBlock.Requirements.Count == 0 && parent.IsChoice)
                        {
                            // Consider this scenario:
                            // (Condition)
                            //     Block!
                            //     >> Option A or B?
                            //     > A
                            //     > B                   <- parent was choice, so disregard one join...?
                            //         Something from B. <- last block was here
                            // (...)                     <- joinLevel is 2. 
                            //     Branch
                            joinLevel -= 1;
                        }
                    }
                    else if (Defines(line, [
                        TokenChar.Situation,
                        TokenChar.ChoiceBlock,
                        TokenChar.MultipleBlock,
                        TokenChar.OnceBlock ]))
                    {
                        if (line.Length > 1 && line[1] == (char)TokenChar.ChoiceBlock)
                        {
                            // Actually a ->
                        }
                        else
                        {
                            // > Hell yeah!
                            //     (LookForFire)
                            //         Yes...?
                            // > Why would I? <- this needs to pop
                            if (_script.CurrentSituation.PeekLastBlock().Conditional)
                            {
                                joinLevel -= 1;

                                // This is a bit awkward, but I added this in cases which:
                                //  -   Option a
                                //      (Something) < parent of this is non linear, so extra pop is needed
                                //          Hello
                                //  -   Option b
                                if (_script.CurrentSituation.PeekLastBlock().NonLinearNode || 
                                    _script.CurrentSituation.PeekLastBlockParent().NonLinearNode)
                                {
                                    _script.CurrentSituation.PopLastBlock();
                                    createJoinBlock = false;
                                }
                            }

                            if (line[0] != (char)TokenChar.MultipleBlock &&
                                line[0] != (char)TokenChar.OnceBlock)
                            {
                                // We might need to check out if we're too deep due to a dialogue choice.
                                // E.g.:
                                //     > Yup.
                                //         Great!
                                //
                                //        >> How is life so far?
                                //        > It's been nice.
                                //            No way.
                                //    > No...                       <- This line needs to pop twice.
                                //        ...
                                int pop = joinLevel;
                                while (pop-- > 0)
                                {
                                    _script.CurrentSituation.PopLastBlock();
                                }

                                createJoinBlock = false;
                            }
                        }
                    }

                    // Depending where we were, we may need to "join" different branches.
                    if (createJoinBlock)
                    {
                        Block? result = _script.CurrentSituation.AddBlock(ConsumePlayUntil(), ConsumeChance(), joinLevel, isNested: false);
                        if (result is null)
                        {
                            OutputHelpers.WriteError($"Unable to join line {index}. Was the indentation correct?");
                            return false;
                        }

                        _currentBlock = result.Id;
                        hasCreatedBlock = true;
                    }
                }
                else if (_indentationIndex > _lastIndentationIndex)
                {
                    // May be used if we end up creating a new block.
                    // (first indent obviously won't count)
                    isNestedBlock = _indentationIndex != 1;

                    // Since the last block was a choice, we will need to create another block to continue it.
                    // AS LONG AS the next block is not another choice!
                    if (_script.CurrentSituation.PeekLastBlock().IsChoice && !(line.Length > 1 && line[1] == (char)TokenChar.ChoiceBlock))
                    {
                        Block? result = _script.CurrentSituation.AddBlock(ConsumePlayUntil(), ConsumeChance(), joinLevel: 0, isNested: true);
                        if (result is null)
                        {
                            OutputHelpers.WriteError($"Unable to nest line {index}. Was the indentation correct?");
                            return false;
                        }

                        _currentBlock = result.Id;
                        isNestedBlock = false;

                        hasCreatedBlock = true;
                    }
                }

                bool isChoiceTitle = isChoice && Defines(line, TokenChar.ChoiceBlock, $"{(char)TokenChar.ChoiceBlock}");
                if (isChoiceTitle && !isNestedBlock)
                {
                    // If this declares another dialog, e.g.:
                    // >> Option?
                    // > Yes
                    // > No!
                    // >> Here comes another...
                    // > Okay.
                    // > Go away!
                    // We need to make sure that the second title declares a new choice block. We do that by popping
                    // the last option and the title.
                    // The popping might have not come up before because they share the same indentation, so that's why we help them a little
                    // bit here.
                    if (_script.CurrentSituation.PeekLastBlock().IsChoice)
                    {
                        _script.CurrentSituation.PopLastBlock();
                        _script.CurrentSituation.PopLastBlock();
                    }
                }
            }

            if (IsDirective(line[0]))
            {
                TokenChar nextDirective = (TokenChar)line[0];

                // Eat this next token!
                line = line.Slice(1);
                column += 1;

                _wasPreviousAction = false;

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

                        if (!_script.AddNewSituation(line))
                        {
                            OutputHelpers.WriteError($"Situation of name '{line}' has been declared twice on line {index}.");
                            OutputHelpers.ProposeFix(index, before: _currentLine, after: $"{_currentLine} 2");

                            return false;
                        }

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
                        //  @order
                        //  @{number}
                        // ...that's all!
                        if (command.StartsWith("random"))
                        {
                            if (hasCreatedBlock)
                            {
                                _ = _script.CurrentSituation.SwitchRelationshipTo(EdgeKind.Random);
                            }
                            else
                            {
                                _random = true;
                            }
                        }
                        else if (command.StartsWith("order"))
                        {
                            // No-op? This is already the default?
                        }
                        else if (TryReadInteger(command) is int number)
                        {
                            if (hasCreatedBlock)
                            {
                                _ = Block.PlayUntil = number;
                            }
                            else
                            {
                                if (Defines(line.Slice(end), TokenChar.BeginCondition, Tokens.Else))
                                {
                                    OutputHelpers.WriteError($"Flow directive '{(char)TokenChar.Flow}' is not supported on else blocks on line {index}.");

                                    ReadOnlySpan<char> newLine = _currentLine.AsSpan().Slice(0, _currentLine.IndexOf('('));
                                    OutputHelpers.ProposeFixOnLineBelow(
                                        index,
                                        _currentLine,
                                        newLine: Regex.Replace(_currentLine, "  @[0-9]", ""),
                                        newLineBelow: string.Concat("    ", newLine));

                                    return false;
                                }

                                Block? result = _script.CurrentSituation.AddBlock(number, ConsumeChance(), joinLevel, isNestedBlock, EdgeKind.Next);
                                if (result is null)
                                {
                                    OutputHelpers.WriteError($"Unable to join line {index}. Was the indentation correct?");
                                    return false;
                                }

                                _currentBlock = result.Id;

                                // Ignore any other join or nested operations, since the block has been dealed with.
                                joinLevel = 0;
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

                        column += end;
                        line = line.Slice(end).TrimStart();

                        break;

                    // (
                    case TokenChar.BeginCondition:
                    {
                        if (!hasCreatedBlock)
                        {
                            int playUntil = ConsumePlayUntil();
                            float chance = ConsumeChance();

                            EdgeKind relationshipKind = EdgeKind.Next;
                            if (line.StartsWith(Tokens.Else))
                            {
                                relationshipKind = EdgeKind.IfElse;
                            }

                            Block? result = _script.CurrentSituation.AddBlock(
                                playUntil, chance, joinLevel, isNestedBlock, relationshipKind);
                            if (result is null)
                            {
                                OutputHelpers.WriteError($"Unable to create condition on line {index}.");
                                return false;
                            }

                            _currentBlock = result.Id;
                        }

                        return ParseConditions(line, index, column);
                    }

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

                        _wasPreviousAction = true;

                        line = line.Slice(0, endAction);
                        return ParseAction(line, index, column);

                    // -
                    case TokenChar.OnceBlock:
                        // Check whether this is actually a '->'
                        if (!line.IsEmpty && line[0] == (char)TokenChar.ChoiceBlock)
                        {
                            line = line.Slice(1);
                            column += 1;

                            return ParseGoto(line, index, column, joinLevel, isNestedBlock);
                        }

                        _playUntil = 1;
                        return ParseOption(line, index, column, joinLevel, isNestedBlock);

                    // +
                    case TokenChar.MultipleBlock:
                        _playUntil = -1;
                        return ParseOption(line, index, column, joinLevel, isNestedBlock);

                    // >
                    case TokenChar.ChoiceBlock:
                        return ParseChoice(line, index, column, joinLevel, isNestedBlock);

                    // %
                    case TokenChar.Chance:
                    {
                        if (!ParseChance(line, index, out float chance, out int endOfChance))
                        {
                            return false;
                        }

                        if (hasCreatedBlock)
                        {
                            Block.Chance = chance;
                        }
                        else
                        {
                            Block? result = _script.CurrentSituation.AddBlock(ConsumePlayUntil(), chance, joinLevel, isNestedBlock, EdgeKind.Next);
                            if (result is null)
                            {
                                OutputHelpers.WriteError($"Unable to join line {index}. Was the indentation correct?");
                                return false;
                            }

                            _currentBlock = result.Id;

                            // Ignore any other join or nested operations, since the block has been dealed with.
                            joinLevel = 0;
                        }

                        column += endOfChance;
                        line = line.Slice(endOfChance);
                        break;
                    }

                    default:
                        return true;
                }
            }
            else
            {
                return ParseLine(line, index, column, isNestedBlock);
            }

            if (!line.IsEmpty)
            {
                return ProcessLine(line, index, column, depth + 1, joinLevel, hasCreatedBlock);
            }

            return true;
        }

        /// <summary>
        /// This parses choices of the dialog. It expects the following line format:
        ///     > Choice is happening
        ///      ^ begin of span    ^ end of span
        ///     >> Choice is happening
        ///      ^ begin of span    ^ end of span
        ///     + > Choice is happening
        ///        ^ begin of span    ^ end of span
        /// </summary>
        private bool ParseChoice(ReadOnlySpan<char> line, int lineIndex, int columnIndex, int joinLevel, bool nested)
        {
            line = line.TrimStart().TrimEnd();

            if (line.IsEmpty)
            {
                OutputHelpers.WriteError($"Invalid empty choice '{(char)TokenChar.ChoiceBlock}' on line {lineIndex}.");
                OutputHelpers.ProposeFixAtColumn(
                    lineIndex,
                    columnIndex,
                    arrowLength: 1,
                    content: _currentLine,
                    issue: "Expected any form of text.");

                return false;
            }

            Block parent = _script.CurrentSituation.PeekBlockAt(joinLevel);
            if (!parent.IsChoice && line[0] != (char)TokenChar.ChoiceBlock)
            {
                ReadOnlySpan<char> newLine = _currentLine.AsSpan().Slice(0, columnIndex);

                OutputHelpers.WriteError($"Expected a title prior to a choice block '{(char)TokenChar.ChoiceBlock}' on line {lineIndex}.");
                OutputHelpers.ProposeFixOnLineAbove(
                    lineIndex,
                    currentLine: _currentLine,
                    newLine: string.Concat(newLine, "> Do your choice"));

                return false;
            }

            if (line[0] == (char)TokenChar.ChoiceBlock)
            {
                // This is actually the title! So trim the first character.
                line = line.Slice(1).TrimStart();
            }

            if (Enum.IsDefined(typeof(TokenChar), (int)line[0]))
            {
                OutputHelpers.WriteWarning($"Special tokens after a '>' will be ignored! Use a '\\' if this was what you meant. See line {lineIndex}.");
                OutputHelpers.ProposeFix(
                    lineIndex,
                    before: _currentLine,
                    after: _currentLine.TrimEnd().Replace($"{line[0]}", $"\\{line[0]}"));
            }

            Block? result = _script.CurrentSituation.AddBlock(ConsumePlayUntil(), ConsumeChance(), joinLevel, nested, EdgeKind.Choice);
            if (result is null)
            {
                OutputHelpers.WriteError($"Unable to create condition on line {lineIndex}. This may happen if you declare an else ('...') without a prior condition, for example.");
                return false;
            }

            _currentBlock = result.Id;

            AddLineToBlock(line);
            return true;
        }

        private bool ParseOption(ReadOnlySpan<char> line, int lineIndex, int columnIndex, int joinLevel, bool nested)
        {
            EdgeKind relationshipKind = EdgeKind.HighestScore;
            if (ConsumeIsRandom() || _script.CurrentSituation.PeekLastEdgeKind() == EdgeKind.Random)
            {
                relationshipKind = EdgeKind.Random;
            }

            // TODO: Check for requirements!

            Block? result = _script.CurrentSituation.AddBlock(ConsumePlayUntil(), ConsumeChance(), joinLevel, nested, relationshipKind);
            if (result is null)
            {
                OutputHelpers.WriteError($"Unable to create option on line {lineIndex}.");
                return false;
            }

            _currentBlock = result.Id;

            line = line.TrimStart();
            if (line.IsEmpty)
            {
                // OutputHelpers.WriteWarning($"Skipping first empty dialog option in line {lineIndex}.");
                return true;
            }

            if (line[0] == (char)TokenChar.Chance)
            {
                line = line[1..];

                if (ParseChance(line, lineIndex, out float chance, out int endOfChance))
                {
                    _chance = chance;
                    line = line.Slice(endOfChance).TrimStart();

                    if (line.IsEmpty)
                    {
                        return true;
                    }
                }
            }

            if (line[0] == (char)TokenChar.BeginCondition)
            {
                // Do not create a new block for conditions. This is because an option is deeply tied
                // to its rules (it's their score, after all!) so we can't just get away with that.
                // We might do another syntax if we want to create a new block for some reason.
                return ParseConditions(line.Slice(1), lineIndex, columnIndex + 1);
            }
            else if (line[0] == (char)TokenChar.ChoiceBlock)
            {
                return ParseChoice(line.Slice(1), lineIndex, columnIndex + 1, joinLevel: 0, nested: false);
            }

            Block.Chance = ConsumeChance();

            AddLineToBlock(line);
            return true;
        }

        private bool CheckAndCreateLinearBlock(int joinLevel, bool isNested)
        {
            // We only create a new block for a line when:
            //  - this is actually the root (or first) node
            //  - previous line was a choice (without a conditional).
            if (_script.CurrentSituation.Blocks.Count == 1 ||
                (isNested && Block.IsChoice && !Block.Conditional))
            {
                Block? result = _script.CurrentSituation.AddBlock(
                    ConsumePlayUntil(), ConsumeChance(), joinLevel, isNested: false, EdgeKind.Next);

                if (result is null)
                {
                    return false;
                }

                _currentBlock = result.Id;
            }

            return true;
        }

        private bool ParseGoto(ReadOnlySpan<char> line, int lineIndex, int currentColumn, int joinLevel, bool nested)
        {
            CheckAndCreateLinearBlock(joinLevel, nested);

            // If the block already has a goto, create another block. E.g.:
            // =entry
            //  @1  -> some
            //
            //  -> talk
            if (_script.CurrentSituation.HasGoto(_currentBlock))
            {
                Block? result = _script.CurrentSituation.AddBlock(ConsumePlayUntil(), ConsumeChance(), joinLevel, nested);
                if (result is null)
                {
                    OutputHelpers.WriteError($"Unable to create option on line {lineIndex}.");
                    return false;
                }

                _currentBlock = result.Id;
            }

            // Check if we started specifying the relationship from the previous requirement.
            ReadOnlySpan<char> location = line.TrimStart().TrimEnd();
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
            // Check for the end of the condition block ')'
            int endColumn = MemoryExtensions.IndexOf(line, (char)TokenChar.EndCondition);
            if (endColumn == -1)
            {
                OutputHelpers.WriteError($"Missing matching '{(char)TokenChar.EndCondition}' on line {lineIndex}.");
                OutputHelpers.ProposeFix(
                    lineIndex,
                    before: _currentLine,
                    after: _currentLine.TrimEnd() + (char)TokenChar.EndCondition);

                return false;
            }

            Block.Conditional = true;
            line = line.Slice(0, endColumn).TrimEnd();

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
        /// Expects to read a float of a line such as:
        ///     ".2 (Something else)"   -> valid
        ///     "..3something"           -> invalid
        ///     ".2"                    -> valid
        /// </summary>
        private float? TryReadFloat(ReadOnlySpan<char> maybeInteger)
        {
            if (float.TryParse(maybeInteger, out float result))
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

        private bool IsDirective(char c) => Enum.IsDefined(typeof(TokenChar), (int)c);
    }
}