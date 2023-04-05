using System.Diagnostics;
using Gum.Utilities;

namespace Gum.InnerThoughts
{
    [DebuggerDisplay("{DebuggerDisplay(),nq}")]
    public readonly struct CriterionNode
    {
        public readonly Criterion Criterion = new();
        public readonly CriterionNodeKind Kind = CriterionNodeKind.And;

        public CriterionNode() { }

        public CriterionNode(Criterion criterion) =>
            Criterion = criterion;

        public CriterionNode(Criterion criterion, CriterionNodeKind kind) =>
            (Criterion, Kind) = (criterion, kind);

        public CriterionNode WithCriterion(Criterion criterion) => new(criterion, Kind);

        public CriterionNode WithKind(CriterionNodeKind kind) => new(Criterion, kind);

        public string DebuggerDisplay()
        {
            return $"{OutputHelpers.ToCustomString(Kind)} {Criterion.DebuggerDisplay()}";
        }
    }
}
