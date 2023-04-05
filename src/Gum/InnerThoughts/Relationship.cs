using System;
using System.Diagnostics;
using System.Text;

namespace Gum.InnerThoughts
{
    /// <summary>
    /// This class has a list of blocks and the respective directions this dialog can take from here.
    /// The relationship kind is how it will pick the next candidate.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay(),nq}")]
    internal class Relationship
    {
        public RelationshipKind Kind = RelationshipKind.Next;

        /// <summary>
        /// List of owners that currently have this relationship.
        /// </summary>
        public readonly List<int> Owners = new();

        /// <summary>
        /// Blocks, in order, which will be subjected to a scan according to <see cref="Kind"/>.
        /// </summary>
        public readonly List<int> Blocks = new();

        public Relationship() { }

        public Relationship(RelationshipKind kind) => Kind = kind;

        public string DebuggerDisplay()
        {
            StringBuilder result = new();

            result = result.Append(
                $"[{Kind}, Blocks = {{");

            bool isFirst = true;
            foreach (int i in Blocks)
            {
                if (!isFirst)
                {
                    result = result.Append(", ");
                }
                else
                {
                    isFirst = false;
                }

                result = result.Append($"{i}");
            }

            result = result.Append($"}}]");

            return result.ToString();
        }
    }
}
