using Gum.InnerThoughts;

namespace Gum
{
    public partial class Parser
    {
        /// <summary>
        /// This will trim the graph: clean up empty edges and such.
        /// </summary>
        private bool Trim()
        {
            IEnumerable<Situation> situations = _script.FetchAllSituations();

            foreach (Situation s in situations)
            {
                (int, Edge)[] edges = s.Edges.Select(t => (t.Key, t.Value)).ToArray();
                foreach ((int owner, Edge edge) in edges)
                {
                    if (edge.Blocks.Count == 0)
                    {
                        s.Edges.Remove(owner);
                    }
                }
            }

            return true;
        }
    }
}
