using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using Gum.Utilities;

namespace Gum.InnerThoughts
{
    [DebuggerDisplay("{Name}")]
    internal class Situation
    {
        public readonly int Id = 0;

        public readonly string Name = string.Empty;

        public readonly List<Block> Blocks = new();

        /// <summary>
        /// The dialogs which will be executed, in order.
        /// </summary>
        public readonly List<int> NextBlocks = new();

        /// <summary>
        /// This points
        /// [ Node Id -> Edge ]
        /// </summary>
        public readonly Dictionary<int, Edge> Edges = new();

        /// <summary>
        /// This points
        /// [ Node Id -> Parent ]
        /// If parent is empty, this is at the top.
        /// </summary>
        public readonly Dictionary<int, HashSet<int>> ParentOf = new();

        /// <summary>
        /// Track the edges when adding those values.
        /// </summary>
        private readonly Stack<Edge> _lastEdges  = new();

        private readonly Stack<int> _lastBlocks = new();

        public Situation() { }

        public Situation(int id, string name)
        {
            Id = id;
            Name = name;

            // Add a root node.
            //Block block = CreateBlock(playUntil: -1, track: true);
            //Edge edge = CreateEdge(RelationshipKind.Next);

            //AssignOwnerToEdge(block.Id, edge);
        }

        public bool SwitchRelationshipTo(RelationshipKind kind)
        {
            if (!_lastEdges.TryPeek(out Edge? lastEdge))
            {
                throw new InvalidOperationException("☠️ Error on the implementation! " +
                    "Why are we assuming a relationship exists here?");
            }
            
            if (lastEdge.Kind == kind)
            {
                // No operation, relationship is already set.
                return true;
            }

            int length = lastEdge.Blocks.Count;
            Debug.Assert(length != 0, "We expect that the last block added will be the block subjected to the " +
                "relationship switch.");

            Edge? edge;
            Block empty;

            switch (kind)
            {
                case RelationshipKind.Next:
                case RelationshipKind.Random:
                    lastEdge.Kind = kind;
                    return true;

                case RelationshipKind.Choice:
                case RelationshipKind.HighestScore:
                    if (length == 1)
                    {
                        lastEdge.Kind = kind;
                    }
                    else
                    {
                        Debug.Fail("I don't understand this scenario fully, please debug this.");

                        // Remove the last block and create a new one?
                    }

                    return true;

                case RelationshipKind.IfElse:
                    // This assumes that the 'else' block has already been inserted into the relationship
                    // and corresponds to the last added block.

                    // First, rearrange our current expectations.
                    if (length == 1)
                    {
                        // Okay, so what happened is that we have an if node point to another node, which is
                        // actually an else.
                        // To fix this, we'll create a ~ghost~ node and use that to create the if/else relationship.
                        // However! We will persist the properties of the first block.
                        if (lastEdge.Owners.Count > 2)
                        {
                            throw new InvalidOperationException("☠️ Error on the implementation! " +
                                "Somehow, you managed to get into a state where a 'else' relationship is grabbing a link from two different" +
                                "nodes. I wasn't sure if this was even possible. Could you report this?");
                        }

                        Block ifBlock = Blocks[lastEdge.Owners[0]];
                        int elseBlock = lastEdge.Blocks[0];

                        _ = _lastBlocks.Pop();
                        _ = _lastBlocks.Pop();

                        _lastBlocks.Push(elseBlock);

                        empty = CreateBlock(playUntil: ifBlock.PlayUntil, track: false);

                        _ = Edges.Remove(ifBlock.Id);
                        _ = _lastEdges.Pop();

                        ReplaceEdgesToNodeWith(ifBlock.Id, empty.Id);

                        edge = CreateEdge(kind);

                        AddNode(edge, ifBlock.Id);
                        AddNode(edge, elseBlock);

                        AssignOwnerToEdge(empty.Id, edge);

                        // We found an else block without any prior block, other than ourselves!
                    }
                    else if (length == 2)
                    {
                        // Only if + else here, all good.
                        lastEdge.Kind = kind;

                        int elseBlock = lastEdge.Blocks[length - 1];

                        _ = _lastBlocks.Pop();
                        _ = _lastBlocks.Pop();

                        _lastBlocks.Push(elseBlock);
                    }
                    else
                    {
                        // We need to get surgical about this.
                        // "Remove" the last block and move it to a new relationship.
                        int ifBlock = lastEdge.Blocks[length - 2];
                        int elseBlock = lastEdge.Blocks[length - 1];

                        RemoveNode(lastEdge, ifBlock);
                        RemoveNode(lastEdge, elseBlock);

                        _ = _lastBlocks.Pop();
                        _ = _lastBlocks.Pop();

                        _lastBlocks.Push(elseBlock);

                        // "Grab" the first block, so it now points to the new relationship created.
                        int previousBlock = lastEdge.Blocks[length - 3];

                        edge = CreateEdge(kind);

                        AddNode(edge, ifBlock);
                        AddNode(edge, elseBlock);

                        AssignOwnerToEdge(previousBlock, edge);
                    }

                    return true;
            }

            return true;
        }

        /// <summary>
        /// Creates a new block subjected to a <paramref name="kind"/> relationship.
        /// </summary>
        public Block AddBlock(int playUntil, bool join, bool isNested, RelationshipKind kind = RelationshipKind.Next)
        {
            // If this should actually join other nodes, make sure we do that.
            if (join && _lastEdges.Count > 0)
            {
                return CreateBlockWithJoin(playUntil);
            }

            // We need to know the "parent" node when nesting blocks (make the parent -> point to the new block).
            bool hasBlocks = _lastBlocks.TryPeek(out int lastBlockId);

            Edge? edge;
            Block block = CreateBlock(playUntil, track: true);

            // If this was called right after a situation has been declared, it'll think that it is a nested block
            // (when it's not really).
            
            if (isNested && hasBlocks)
            {
                // If there is a parent block that this can be nested to.
                if (hasBlocks)
                {
                    if (!Edges.TryGetValue(lastBlockId, out edge))
                    {
                        // The parent does not have an edge to other nodes. In that case, create one.
                        edge = CreateEdge(kind);
                        AssignOwnerToEdge(lastBlockId, edge);
                    }

                    AddNode(edge, block.Id);
                }
                else
                {
                    edge = CreateEdge(RelationshipKind.Next);
                    AddNode(edge, block.Id);

                    AssignOwnerToEdge(lastBlockId, edge);

                    if (edge.Kind != RelationshipKind.Next)
                    {
                        edge = CreateEdge(kind);
                        AssignOwnerToEdge(block.Id, edge);
                    }
                }
            }
            else if (!hasBlocks)
            {
                // This is actually a "root" node. Add it directly as the next available node and create a relationship.
                NextBlocks.Add(block.Id);

                edge = CreateEdge(kind);
                AssignOwnerToEdge(block.Id, edge);

                // This is actually a non-sequential node, so it can't be simply tracked in "NextBlocks".
                // TODO: Remove NextBlocks?? I don't know why I added that.
                if (!kind.IsSequential())
                {
                    block = CreateBlock(playUntil, track: false);
                    AddNode(edge, block.Id);
                }
            }
            else
            {
                Edge targetEdge = _lastEdges.Peek();

                if (kind == RelationshipKind.Choice && _lastBlocks.Count > 2)
                {
                    int parent = _lastBlocks.ElementAt(2);
                    if (Edges.TryGetValue(parent, out edge) && 
                        edge.Kind == RelationshipKind.Choice)
                    {
                        targetEdge = edge;
                    }
                }

                if (targetEdge.Kind != kind && targetEdge.Blocks.Count == 0 &&
                    NextBlocks.Contains(lastBlockId) && kind == RelationshipKind.Next)
                {
                    _lastEdges.Clear();
                    _lastBlocks.Clear();
                    _lastBlocks.Push(block.Id);

                    // This is actually a "root" node. Add it directly as the next available node and create a relationship.
                    NextBlocks.Add(block.Id);

                    edge = CreateEdge(kind);
                    AssignOwnerToEdge(block.Id, edge);

                    // This is actually a non-sequential node, so it can't be simply tracked in "NextBlocks".
                    // TODO: Remove NextBlocks?? I don't know why I added that.
                    if (!kind.IsSequential())
                    {
                        block = CreateBlock(playUntil, track: false);
                        AddNode(edge, block.Id);
                    }

                    return block;
                }

                if (kind == RelationshipKind.IfElse && targetEdge.Kind != kind)
                {
                    if (targetEdge.Kind != RelationshipKind.Next)
                    {
                        _ = _lastBlocks.TryPop(out _);
                        _ = _lastBlocks.TryPop(out _);

                        _lastBlocks.Push(block.Id);

                        _ = _lastEdges.TryPop(out _);

                        Block empty = CreateBlock(playUntil: -1, track: false);
                        ReplaceEdgesToNodeWith(lastBlockId, empty.Id);

                        targetEdge = CreateEdge(kind);

                        AddNode(targetEdge, lastBlockId);
                        AddNode(targetEdge, block.Id);
                        AssignOwnerToEdge(empty.Id, targetEdge);

                        return block;
                    }

                    // This has been a bit of a headache, but if this is an "if else" and the current connection
                    // is not the same, we'll convert this later.
                    kind = RelationshipKind.Next;
                }
                // If this is "intruding" an existing nested if-else.
                //  (hasA)
                //      (hasB)
                //      (...)
                //  (...)
                else if (kind == RelationshipKind.IfElse && targetEdge.Kind == kind && lastBlockId == targetEdge.Owners[0])
                {
                    // This is copied and pasted from above. We will refactor this.
                    _ = _lastBlocks.TryPop(out _);
                    _ = _lastBlocks.TryPop(out _);

                    _lastBlocks.Push(block.Id);

                    _ = _lastEdges.TryPop(out _);

                    Block empty = CreateBlock(playUntil: -1, track: false);
                    ReplaceEdgesToNodeWith(lastBlockId, empty.Id);

                    targetEdge = CreateEdge(kind);

                    AddNode(targetEdge, lastBlockId);
                    AddNode(targetEdge, block.Id);
                    AssignOwnerToEdge(empty.Id, targetEdge);

                    return block;
                }

                if (kind == RelationshipKind.HighestScore && targetEdge.Kind == RelationshipKind.Random)
                {
                    // A "HighestScore" kind when is matched with a "random" relationship, it is considered one of them
                    // automatically.
                    kind = RelationshipKind.Random;
                }

                if (targetEdge.Kind != kind && targetEdge.Blocks.Count == 0)
                {
                    targetEdge.Kind = kind;
                }
                else if (targetEdge.Kind != kind && kind == RelationshipKind.HighestScore)
                {
                    targetEdge = CreateEdge(kind);

                    AssignOwnerToEdge(lastBlockId, targetEdge);
                }
                else if (targetEdge.Kind != kind)
                {
                    _ = _lastEdges.Pop();

                    Block lastBlock = Blocks[lastBlockId];

                    Block empty = CreateBlock(playUntil: lastBlock.PlayUntil, track: false);
                    ReplaceEdgesToNodeWith(lastBlockId, empty.Id);

                    targetEdge = CreateEdge(kind);

                    AddNode(targetEdge, lastBlock.Id);
                    AssignOwnerToEdge(empty.Id, targetEdge);
                }

                AddNode(targetEdge, block.Id);
            }

            return block;
        }

        /// <summary>
        /// Find all leaf nodes eligible to be joined.
        /// This will disregard nodes that are already dead (due to a goto!).
        /// </summary>
        private void FindAllLeaves(int block, ref List<int> result)
        {
            if (Edges.TryGetValue(block, out Edge? relationship))
            {
                foreach (int otherBlock in relationship.Blocks)
                {
                    FindAllLeaves(otherBlock, ref result);
                }
            }
            else
            {
                if (!_blocksWithGoto.Contains(block))
                {
                    // This doesn't point to any other blocks - so it's a leaf!
                    result.Add(block);
                }
            }
        }

        /// <summary>
        /// Creates a block by joining whatever blocks were in the last relationship and point to it.
        /// </summary>
        private Block CreateBlockWithJoin(int playUntil)
        {
            Debug.Assert(_lastEdges.Count != 0, "This should only be called with a previous relationship.");

            bool hasAnyBlocks = _lastBlocks.Count != 0;

            // Get rid of the last blocks prior to the join, if any.
            if (hasAnyBlocks)
            {
                _ = _lastBlocks.TryPop(out _);
                _ = _lastBlocks.TryPop(out _);
            }

            Block joinedBlock = CreateBlock(playUntil, track: true);

            if (!hasAnyBlocks && _lastEdges.Count == 1)
            {
                NextBlocks.Add(joinedBlock.Id);
            }

            List<int> blocksThatWillBeJoined = _lastEdges.Peek().Blocks;
            List<int> leafBlocks = new();

            Edge lastRelationship = _lastEdges.Pop();

            // Now, for each of those blocks, we'll collect all of its leaves and add edges to it.
            foreach (int blockToJoin in blocksThatWillBeJoined)
            {
                FindAllLeaves(blockToJoin, ref leafBlocks);
            }

            Edge edge;
            if (leafBlocks.Count == 0)
            {
                Debug.Assert(lastRelationship.Owners.Count == 1, "If this is not the case, consider refactoring.");

                _ = _lastBlocks.Pop();

                int owner = lastRelationship.Owners[0];
                Block empty = CreateBlock(playUntil: Blocks[owner].PlayUntil, track: true);
                ReplaceEdgesToNodeWith(owner, empty.Id);

                _lastBlocks.Push(joinedBlock.Id);

                edge = CreateEdge(RelationshipKind.Next, stack: true);

                AddNode(edge, owner);
                AddNode(edge, joinedBlock.Id);

                AssignOwnerToEdge(empty.Id, edge);
            }
            else
            {
                // Start by creating a link between previous blocks and this new one.
                edge = CreateEdge(RelationshipKind.Next, stack: false);
                AddNode(edge, joinedBlock.Id);

                foreach (int i in leafBlocks)
                {
                    AssignOwnerToEdge(i, edge);
                }
            }

            // Finally, create a new relationship for the block itself.
            edge = CreateEdge(RelationshipKind.Next);
            AssignOwnerToEdge(joinedBlock.Id, edge);

            return joinedBlock;
        }

        /// <summary>
        /// Creates a new block and assign an id to it.
        /// </summary>
        private Block CreateBlock(int playUntil, bool track)
        {
            int id = Blocks.Count;
            Block block = new(id, playUntil);

            Blocks.Add(block);

            if (track)
            {
                _lastBlocks.Push(id);
            }

            ParentOf[id] = new();

            return block;
        }

        private Edge CreateEdge(RelationshipKind kind, bool stack = true)
        {
            Edge relationship = new(kind);

            if (stack)
            {
                _lastEdges.Push(relationship);
            }

            return relationship;
        }

        private void AssignOwnerToEdge(int id, Edge edge)
        {
            Edges.Add(id, edge);
            edge.Owners.Add(id);

            // Track parents.
            foreach (int block in edge.Blocks)
            {
                ParentOf[block].Add(id);
            }
        }

        private void AddNode(Edge edge, int id)
        {
            edge.Blocks.Add(id);

            // Track parents.
            foreach (int owner in edge.Owners)
            {
                ParentOf[id].Add(owner);
            }
        }

        private void RemoveNode(Edge edge, int id)
        {
            edge.Blocks.Remove(id);

            foreach (int owner in edge.Owners)
            {
                ParentOf[id].Remove(owner);
            }
        }

        /// <summary>
        /// Given id: C and other: D:
        /// Before:
        ///    A      D
        ///   / \
        ///  B   C 
        ///  
        ///    D
        /// After:
        ///    A      C
        ///   / \
        ///  B   D 
        /// This assumes that <paramref name="other"/> is an orphan.
        /// <paramref name="id"/> will be orphan after this.
        /// </summary>
        private void ReplaceEdgesToNodeWith(int id, int other)
        {
            if (ParentOf[id].Count == 0)
            {
                // This is actually the root: easy, just replace with other.
                int position = NextBlocks.IndexOf(id);
                NextBlocks[position] = other;

                return;
            }

            foreach (int parent in ParentOf[id])
            {
                // Manually tell each parent that the child has stopped existing.
                int position = Edges[parent].Blocks.IndexOf(id);
                Edges[parent].Blocks[position] = other;

                ParentOf[other].Add(parent);
            }

            ParentOf[id].Clear();
        }

        public void PopLastBlock()
        {
            if (_lastBlocks.Count == 0)
            {
                return;
            }

            _ = _lastBlocks.Pop();
        }

        public void PopLastRelationship()
        {
            if (!_lastEdges.TryPeek(out Edge? relationship))
            {
                // No op.
                return;
            }

            if (relationship.Blocks.Count == 0)
            {
                _ = _lastEdges.Pop();
                _ = _lastBlocks.Pop();
            }
            else
            {
                foreach (int i in relationship.Blocks)
                {
                    if (_lastBlocks.TryPeek(out int top) && i == top)
                    {
                        _ = _lastBlocks.Pop();
                    }
                }

                if (relationship.Kind == RelationshipKind.Random)
                {
                    _ = _lastEdges.Pop();
                    _ = _lastBlocks.Pop();
                }
                else if (_lastEdges.Count >= 2 && _lastEdges.ElementAt(1).Kind == RelationshipKind.Choice)
                {
                    _ = _lastEdges.Pop();
                    _ = _lastBlocks.Pop();
                }
            }
        }

        private readonly HashSet<int> _blocksWithGoto = new();

        public void MarkGotoOnBlock(int block, bool isExit)
        {
            if (isExit)
            {
                Blocks[block].Exit();
            }

            _ = _blocksWithGoto.Add(block);
        }
    }
}
