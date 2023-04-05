using Gum.InnerThoughts;

namespace Gum.Utilities
{
    internal static class InnerThoughtsHelpers
    {
        /// <summary>
        /// Returns whether the order in which the blocks are available within an edge
        /// is relevant or not when picking a new option.
        /// </summary>
        public static bool IsSequential(this RelationshipKind kind)
        {
            switch (kind)
            {
                case RelationshipKind.Next:
                case RelationshipKind.IfElse:
                    return true;

                case RelationshipKind.Random:
                case RelationshipKind.HighestScore:
                case RelationshipKind.Choice:
                    return false;
            }

            return false;
        }
    }
}
