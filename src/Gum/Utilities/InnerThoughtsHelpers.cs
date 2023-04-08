using Gum.InnerThoughts;

namespace Gum.Utilities
{
    internal static class InnerThoughtsHelpers
    {
        /// <summary>
        /// Returns whether the order in which the blocks are available within an edge
        /// is relevant or not when picking a new option.
        /// </summary>
        public static bool IsSequential(this EdgeKind kind)
        {
            switch (kind)
            {
                case EdgeKind.Next:
                case EdgeKind.IfElse:
                case EdgeKind.Choice:
                    return true;

                case EdgeKind.Random:
                case EdgeKind.HighestScore:
                    return false;
            }

            return false;
        }
    }
}
