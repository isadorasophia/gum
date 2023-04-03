using Whispers.Attributes;

namespace Whispers.InnerThoughts
{
    public enum CriterionNodeKind
    {
        [TokenName("and")]
        And,

        [TokenName("or")]
        Or
    }
}
