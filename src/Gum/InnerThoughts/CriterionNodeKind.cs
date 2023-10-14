using Gum.Attributes;

namespace Gum.InnerThoughts
{
    public enum CriterionNodeKind
    {
        [TokenName("and")]
        And,

        [TokenName("or")]
        Or
    }
}