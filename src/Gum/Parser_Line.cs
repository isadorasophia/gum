using Gum.InnerThoughts;
using Gum.Utilities;
using System;
using System.Data.Common;
using System.Reflection;

namespace Gum
{
    internal static partial class Tokens
    {
        public const char EscapeToken = '\\';
        public const char SpeakerToken = ':';
        public const char SpeakerPortraitToken = '.';
    }

    public partial class Parser
    {
        private bool ParseLine(ReadOnlySpan<char> line, int index, int _, bool isNested)
        {
            if (_wasPreviousAction)
            {
                // If current line is not an action *but* the previous line was,
                // create a block so this can be executed immediately after this line.

                Block? result = _script.CurrentSituation.AddBlock(ConsumePlayUntil(), ConsumeChance(), joinLevel: 0, isNested: false);
                if (result is null)
                {
                    OutputHelpers.WriteError($"Unable to add condition on line {index}. Was the indentation correct?");
                    return false;
                }

                _currentBlock = result.Id;
                _wasPreviousAction = false;
            }

            CheckAndCreateLinearBlock(joinLevel: 0, isNested);

            // This is probably just a line! So let's just read as it is.
            AddLineToBlock(line);
            return true;
        }

        private void AddLineToBlock(ReadOnlySpan<char> line)
        {
            (string? speaker, string? portrait) = ReadSpeakerAndLine(line, out int end);
            if (end != -1)
            {
                line = line.Slice(end + 1);
            }

            line = line.TrimStart().TrimEnd();

            Block.AddLine(speaker, portrait, line.ToString().Replace("\\", ""));
        }

        /// <summary>
        /// Read an optional chance argument, expects to receive a line:
        ///     %10 {line}
        ///      ^
        /// </summary>
        /// <param name="line">Line.</param>
        /// <param name="end">If none, returns -1.</param>
        private bool ParseChance(ReadOnlySpan<char> line, int index, out float chance, out int end)
        {
            chance = 1;
            end = -1;

            ReadOnlySpan<char> sChance = GetNextWord(line, out int endOfNumber);
            if (sChance.Length == 0 || TryReadInteger(sChance) is not int parsedChance)
            {
                OutputHelpers.WriteError($"Chance '{(char)TokenChar.Chance}' does not have a valid number on line {index}.");
                return false;
            }

            chance = parsedChance == 0 ? 0 : parsedChance / 100f;
            end = endOfNumber + 1;

            return true;
        }

        /// <summary>
        /// Read an optional string for the speaker and portrait in a line.
        /// Valid examples:
        ///     {speaker}.{portrait}: {line}
        ///     {speaker}: {line}
        ///     {line}
        /// </summary>
        private (string? Speaker, string? Portrait) ReadSpeakerAndLine(ReadOnlySpan<char> line, out int end)
        {
            string? speaker = null;
            string? portrait = null;

            end = -1;

            if (line.IsEmpty)
            {
                return (speaker, portrait);
            }

            ReadOnlySpan<char> speakerText = GetNextWord(line, out end);
            if (end == -1)
            {
                return (speaker, portrait);
            }

            // First, check if there is a speaker specified and look for the escape token.
            end = speakerText.IndexOf(Tokens.SpeakerToken);
            if (end == -1 || speakerText[end - 1] == Tokens.EscapeToken)
            {
                return (speaker, portrait);
            }

            speakerText = speakerText.Slice(0, end);

            int portraitEndIndex = speakerText.IndexOf(Tokens.SpeakerPortraitToken);
            if (portraitEndIndex == -1)
            {
                return (speakerText.ToString(), portrait);
            }

            speaker = speakerText.Slice(0, portraitEndIndex).ToString();
            portrait = speakerText.Slice(portraitEndIndex + 1).ToString();

            return (speaker, portrait);
        }
    }
}