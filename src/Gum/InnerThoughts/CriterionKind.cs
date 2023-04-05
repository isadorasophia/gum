using Gum.Attributes;

namespace Gum.InnerThoughts
{
    public enum CriterionKind
    {
        [TokenName("is")]
        Is,             // Boolean, integer or string comparison
        [TokenName("<")]
        Less,           // Integer comparison
        [TokenName("<=")]
        LessOrEqual,    // Integer comparison
        [TokenName(">")]
        Bigger,         // Integer comparison
        [TokenName(">=")]
        BiggerOrEqual,  // Integer comparison
        [TokenName("!=")]
        Different       // Boolean, integer or string comparison
    }
}
