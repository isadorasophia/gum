using System;
using System.Diagnostics;
using System.Text;

namespace Whispers.InnerThoughts
{
    [DebuggerDisplay("{DebuggerDisplay(),nq}")]
    internal class Block
    {
        public readonly int Id = 0;

        /// <summary>
        /// Stop playing this dialog until this number.
        /// If -1, this will play forever.
        /// </summary>
        public readonly int PlayUntil = -1;

        public readonly List<CriterionNode> Requirements = new();

        public readonly List<Line> Lines = new();

        public List<DialogAction>? Actions = null;

        /// <summary>
        /// Go to another dialog with a specified id.
        /// </summary>
        public readonly int? GoTo = null;

        public Block() { }

        public Block(int id) { Id = id; }

        public Block(int id, int playUntil) { (Id, PlayUntil) = (id, playUntil); }

        public void AddLine(ReadOnlySpan<char> text)
        {
            Lines.Add(new(Line.OWNER, text.ToString()));
        }

        public void AddRequirement(CriterionNode node)
        {
            Requirements.Add(node);
        }

        public void AddAction(DialogAction action)
        {
            Actions ??= new();
            Actions.Add(action);
        }

        public string DebuggerDisplay()
        {
            StringBuilder result = new();
            _ = result.Append(
                $"[{Id}, Requirements = {Requirements.Count}, Lines = {Lines.Count}, Actions = {Actions?.Count ?? 0}]");

            return result.ToString();
        }
    }
}
