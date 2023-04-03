using Whispers.Attributes;

namespace Whispers.Blackboards
{
    public enum BlackboardActionKind
    {
        [TokenName("=")]
        Set,      // All

        [TokenName("+=")]
        Add,      // Integer

        [TokenName("-=")]
        Minus,    // Integer

        [TokenName("c:")]
        Component // Adding or modifying components.
    }
}
