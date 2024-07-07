﻿using Gum.Utilities;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Gum.InnerThoughts
{
    [DebuggerDisplay("{Name}")]
    public class Situation
    {
        [JsonInclude]
        public readonly int Id = 0;

        [JsonInclude]
        public readonly string Name = string.Empty;

        public int Root = 0;

        public readonly List<Block> Blocks = new();

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

        private readonly Stack<int> _lastBlocks = new();

        public Situation() { }

        public Situation(int id, string name)
        {
            Id = id;
            Name = name;

            // Add a root node.
            Block block = CreateBlock(playUntil: -1, chance: 1, track: true);
            Edge edge = CreateEdge(EdgeKind.Next);

            AssignOwnerToEdge(block.Id, edge);

            Root = block.Id;
        }

        public bool SwitchRelationshipTo(EdgeKind kind)
        {
            Edge lastEdge = LastEdge;

            if (lastEdge.Kind == kind)
            {
                // No operation, relationship is already set.
                return true;
            }

            int length = lastEdge.Blocks.Count;

            switch (kind)
            {
                case EdgeKind.Next:
                case EdgeKind.Random:
                    lastEdge.Kind = kind;
                    return true;

                case EdgeKind.Choice:
                case EdgeKind.HighestScore:
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

                case EdgeKind.IfElse:
                    return false;
            }

            return true;
        }

        internal EdgeKind PeekLastEdgeKind() => LastEdge.Kind;

        internal Block PeekLastBlock() => Blocks[_lastBlocks.Peek()];

        internal Block PeekLastBlockParent() => Blocks[_lastBlocks.ElementAt(1)];

        internal Block PeekBlockAt(int level)
        {
            int index;

            try
            {
                index = _lastBlocks.ElementAt(level);
            }
            catch
            {
                _ = _lastBlocks.TryPeek(out index);
            }

            return Blocks[index];
        }

        internal EdgeKind PeekEdgeAtKind(int level) => Edges[_lastBlocks.ElementAt(level)].Kind;

        /// <summary>
        /// Creates a new block subjected to a <paramref name="kind"/> relationship.
        /// </summary>
        public Block? AddBlock(int playUntil, float chance, int joinLevel, bool isNested, EdgeKind kind = EdgeKind.Next)
        {
            Block lastBlock = PeekLastBlock();
            if (joinLevel == 0 && playUntil == 1 && lastBlock.PlayUntil == 1 && !lastBlock.NonLinearNode)
            {
                // Consider this:
                //     @1  -> Go
                //
                //     @1  Some other dialog with the same indentation.
                //         -> exit!
                // We need to "fake" another join level here to make up for our lack of indentation.
                joinLevel += 1;
            }

            // We need to know the "parent" node when nesting blocks (make the parent -> point to the new block).
            (int parentId, int[] blocksToBeJoined) = FetchParentOfJoinedBlock(joinLevel, kind);

            Edge lastEdge = Edges[parentId];

            // Looks on whether we need to pop nodes on:
            //  >> Dialog
            //  > Choice
            //  > Choice 2 <- pop here!
            bool shouldPopChoiceBlock = kind == EdgeKind.Choice && Blocks[parentId].IsChoice && lastEdge.Kind != kind && !isNested;

            if (shouldPopChoiceBlock || (!kind.IsSequential() && Blocks[parentId].NonLinearNode))
            {
                // This is the only "HACKY" thing I will allow here.
                // Since I want to avoid a syntax such as:
                //  (condition)
                //      @score
                //          - something
                //          - something
                // and instead have something like
                //  (condition)
                //      - something
                //      - something
                // I will do the following:
                _lastBlocks.Pop();

                parentId = _lastBlocks.Peek();
                blocksToBeJoined = new int[] { parentId };
            }

            // Do not make a join on the leaves if this is an (...) or another choice (-/+)
            if (kind == EdgeKind.IfElse ||
                (!lastEdge.Kind.IsSequential() && !kind.IsSequential()) ||
                (kind == EdgeKind.Choice && lastEdge.Kind == EdgeKind.Choice))
            {
                blocksToBeJoined = [parentId];
            }

            Block block = CreateBlock(playUntil, chance, track: true);

            block.NonLinearNode = !kind.IsSequential();
            block.IsChoice = kind == EdgeKind.Choice;

            Edge? edge = CreateEdge(EdgeKind.Next);
            AssignOwnerToEdge(block.Id, edge);

            // If this was called right after a situation has been declared, it'll think that it is a nested block
            // (when it's not really).

            if (isNested)
            {
                Edge? parentEdge = Edges[parentId];

                if (block.IsChoice)
                {
                    parentEdge.Kind = EdgeKind.Next;

                    //  (Condition)
                    //      >> Nested title
                    //      > Option a
                    //      > Option b
                    edge.Kind = kind;
                }
                else
                {
                    parentEdge.Kind = kind;
                }

                AddNode(parentEdge, block.Id);

                return block;
            }

            foreach (int parent in blocksToBeJoined)
            {
                JoinBlock(block, edge, parent, kind);
            }

            return block;
        }

        private bool JoinBlock(Block block, Edge nextEdge, int parentId, EdgeKind kind)
        {
            Edge lastEdge = Edges[parentId];
            switch (kind)
            {
                case EdgeKind.Choice:
                    if (lastEdge.Kind != EdgeKind.Choice)
                    {
                        // before:
                        //     .
                        //     |    D
                        //     A 
                        //
                        // after:
                        //     .
                        //     |
                        //     A
                        //     |
                        //     D
                        //  {choice}

                        lastEdge = Edges[parentId];
                        AddNode(lastEdge, block.Id);

                        nextEdge.Kind = kind;

                        return true;
                    }

                    AddNode(lastEdge, block.Id);
                    return true;

                case EdgeKind.HighestScore:
                case EdgeKind.Random:
                    // nextEdge.Kind = EdgeKind.HighestScore;
                    break;

                case EdgeKind.IfElse:
                    if (lastEdge.Kind == EdgeKind.IfElse ||
                        (lastEdge.Kind.IsSequential() && lastEdge.Blocks.Count == 1))
                    {
                        lastEdge.Kind = EdgeKind.IfElse;

                        AddNode(lastEdge, block.Id);
                        return true;
                    }

                    if (lastEdge.Blocks.Count > 1)
                    {
                        CreateDummyNodeAt(lastEdge.Blocks.Last(), block.Id, kind);
                        return true;
                    }

                    Debug.Fail("Empty edge?");
                    return false;
            }

            if (kind == EdgeKind.IfElse && lastEdge.Kind != kind)
            {
                if (lastEdge.Kind != EdgeKind.Next)
                {
                    CreateDummyNodeAt(parentId, block.Id, kind);
                    return true;
                }

                // This has been a bit of a headache, but if this is an "if else" and the current connection
                // is not the same, we'll convert this later.
                kind = EdgeKind.Next;
            }

            // If this is "intruding" an existing nested if-else.
            //  (hasA)
            //      (hasB)
            //      (...)
            //  (...)
            else if (kind == EdgeKind.IfElse && lastEdge.Kind == kind && parentId == lastEdge.Owner)
            {
                CreateDummyNodeAt(parentId, block.Id, kind);
                return true;
            }

            if (kind == EdgeKind.HighestScore && lastEdge.Kind == EdgeKind.Random)
            {
                // A "HighestScore" kind when is matched with a "random" relationship, it is considered one of them
                // automatically.
                kind = EdgeKind.Random;
            }

            if (lastEdge.Kind != kind && lastEdge.Blocks.Count == 0)
            {
                lastEdge.Kind = kind;
            }
            else if (lastEdge.Kind != kind && kind == EdgeKind.HighestScore)
            {
                // No-op?
            }
            else if (lastEdge.Kind != kind)
            {
                CreateDummyNodeAt(parentId, block.Id, kind);
                return true;
            }

            AddNode(lastEdge, block.Id);
            return true;
        }

        /// <summary>
        /// Given C and D:
        /// Before:
        ///    A
        ///   / \
        ///  B   C <- parent  D <- block
        ///     /
        ///  ...
        ///  
        /// After:
        ///    A
        ///   / \
        ///  B   E <- dummy
        ///     / \
        ///    C   D
        ///   /    
        ///...
        ///
        /// </summary>
        private void CreateDummyNodeAt(int parentId, int blockId, EdgeKind kind)
        {
            Block lastBlock = Blocks[parentId];

            // Block the last block, this will be the block that we just added (blockId).
            _ = _lastBlocks.TryPop(out _);

            // If this block corresponds to the parent, remove it from the stack.
            if (_lastBlocks.TryPeek(out int peek) && peek == parentId)
            {
                _ = _lastBlocks.Pop();
            }

            Block empty = CreateBlock(playUntil: lastBlock.PlayUntil, chance: lastBlock.Chance, track: true);
            ReplaceEdgesToNodeWith(parentId, empty.Id);

            _lastBlocks.Push(blockId);

            Edge lastEdge = CreateEdge(kind);
            AddNode(lastEdge, parentId);
            AddNode(lastEdge, blockId);
            AssignOwnerToEdge(empty.Id, lastEdge);
        }

        /// <summary>
        /// Find all leaf nodes eligible to be joined.
        /// This will disregard nodes that are already dead (due to a goto!).
        /// </summary>
        private void GetAllLeaves(int block, bool createBlockForElse, ref HashSet<int> result)
        {
            Edge edge = Edges[block];
            if (edge.Blocks.Count != 0)
            {
                foreach (int otherBlock in edge.Blocks)
                {
                    GetAllLeaves(otherBlock, createBlockForElse, ref result);
                }

                if (createBlockForElse)
                {
                    //  @1  (Something)
                    //          Hello!
                    //
                    //      (...Something2)
                    //          Hello once?
                    //  Bye.
                    //
                    // turns into:
                    //  @1  (Something)
                    //          Hello!
                    //
                    //      (...Something2)
                    //          Hello once?
                    //
                    //      (...)
                    //          // go down.
                    //
                    //  Bye.
                    // If this an else if, but may not enter any of the blocks,
                    // do a last else to the next block.
                    if (edge.Kind == EdgeKind.IfElse &&
                        Blocks[edge.Blocks.Last()].Requirements.Count != 0)
                    {
                        // Create else block and its edge.
                        int elseBlockId = CreateBlock(-1, chance: 1, track: false).Id;

                        Edge? elseBlockEdge = CreateEdge(EdgeKind.Next);
                        AssignOwnerToEdge(elseBlockId, elseBlockEdge);

                        // Assign the block as part of the .IfElse edge.
                        AddNode(edge, elseBlockId);

                        // Track the block as a leaf.
                        result.Add(elseBlockId);
                    }
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

        private (int Parent, int[] blocksToBeJoined) FetchParentOfJoinedBlock(int joinLevel, EdgeKind edgeKind)
        {
            int topParent;

            if (joinLevel == 0)
            {
                topParent = _lastBlocks.Peek();

                return (topParent, new int[] { topParent });
            }

            while (_lastBlocks.Count > 1 && joinLevel-- > 0)
            {
                int blockId = _lastBlocks.Pop();
                Block block = Blocks[blockId];

                // When I said I would allow one (1) hacky code, I lied.
                // This is another one.
                // SO, the indentation gets really weird for conditionals, as we pretty
                // much disregard one indent? I found that the best approach to handle this is 
                // manually cleaning up the stack when there is NOT a conditional block on join.
                // This is also true for @[0-9] blocks, since those add an extra indentation.
                if (block.NonLinearNode)
                {
                    if (block.Conditional)
                    {
                        // Nevermind the last pop: this is actually not an indent at all, as the last
                        // block was actually a condition (and we have a minus one indent).
                        _lastBlocks.Push(blockId);
                    }
                    else if (Edges[ParentOf[blockId].First()].Kind != EdgeKind.HighestScore)
                    {
                        // Skip indentation for non linear nodes with the default setting.
                        // TODO: Check how that breaks join with @order? Does it actually break?
                        // no-op.
                    }
                    else
                    {
                        joinLevel++;
                    }
                }
                else if (!block.Conditional)
                {
                    // [parent]
                    //  @1  Conditional
                    //      Line
                    //
                    //  And the other block.
                    //  but not here:
                    //  >> something
                    //  > a
                    //      >> b
                    //      > c
                    //  > d <-
                    if (block.PlayUntil == -1 && (!block.IsChoice || edgeKind != EdgeKind.Choice))
                    {
                        joinLevel++;
                    }
                }
            }

            topParent = _lastBlocks.Peek();

            int[] blocksToLookForLeaves = Edges[topParent].Blocks.ToArray();
            HashSet<int> leafBlocks = new();

            // Now, for each of those blocks, we'll collect all of its leaves and add edges to it.
            foreach (int blockToJoin in blocksToLookForLeaves)
            {
                GetAllLeaves(blockToJoin, createBlockForElse: edgeKind != EdgeKind.IfElse, ref leafBlocks);
            }

            leafBlocks.Add(topParent);

            if (leafBlocks.Count != 0)
            {
                HashSet<int> prunnedLeafBlocks = leafBlocks.ToHashSet();
                foreach (int b in prunnedLeafBlocks)
                {
                    if (b != topParent)
                    {
                        // Whether this block will always be played or is it tied to a condition.
                        // If this is tied to the root directly, returns -1.
                        int conditionalParent = GetConditionalBlock(b);
                        if (conditionalParent == -1 || conditionalParent == topParent)
                        {
                            prunnedLeafBlocks.Remove(topParent);
                        }
                    }

                    // If the last block doesn't have any condition *but* this is actually an 
                    // if else block.
                    // I *think* this doesn't take into account child of child blocks, but it's not
                    // the end of the world if we have an extra edge that will never be reached.
                    switch (Edges[b].Kind)
                    {
                        case EdgeKind.IfElse:
                            if (Edges[b].Blocks.LastOrDefault() is int lastBlockId)
                            {
                                if (Blocks[lastBlockId].Requirements.Count == 0 &&
                                    prunnedLeafBlocks.Contains(lastBlockId))
                                {
                                    prunnedLeafBlocks.Remove(b);
                                }
                            }

                            break;

                        case EdgeKind.HighestScore:
                        case EdgeKind.Choice:
                        case EdgeKind.Random:
                            prunnedLeafBlocks.Remove(b);
                            break;
                    }
                }

                leafBlocks = prunnedLeafBlocks;
            }

            return (topParent, leafBlocks.ToArray());
        }

        private int GetConditionalBlock(int block)
        {
            if (Blocks[block].PlayUntil != -1)
            {
                return block;
            }

            if (Blocks[block].Requirements.Count != 0)
            {
                return block;
            }

            if (ParentOf[block].Contains(0))
            {
                // This is tied to the root and the block can play forever.
                return -1;
            }

            int result = -1;
            foreach (int parent in ParentOf[block])
            {
                result = GetConditionalBlock(parent);
                if (result == -1)
                {
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Creates a new block and assign an id to it.
        /// </summary>
        private Block CreateBlock(int playUntil, float chance, bool track)
        {
            int id = Blocks.Count;
            Block block = new(id, playUntil, chance);

            Blocks.Add(block);

            if (track)
            {
                _lastBlocks.Push(id);
            }

            ParentOf[id] = new();

            return block;
        }

        private Edge CreateEdge(EdgeKind kind)
        {
            Edge relationship = new(kind);

            return relationship;
        }

        private void AssignOwnerToEdge(int id, Edge edge)
        {
            Edges.Add(id, edge);
            edge.Owner = id;

            // Track parents.
            foreach (int block in edge.Blocks)
            {
                ParentOf[block].Add(id);
            }
        }

        private void AddNode(Edge edge, int id)
        {
            edge.Blocks.Add(id);

            if (edge.Owner != -1)
            {
                ParentOf[id].Add(edge.Owner);
            }
        }

        /// <summary>
        /// Given C and D:
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
            if (Root == id)
            {
                Root = other;
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

        private bool IsParentOf(int parentNode, int childNode)
        {
            if (ParentOf[childNode].Count == 0)
            {
                return false;
            }

            if (ParentOf[childNode].Contains(parentNode))
            {
                return true;
            }

            foreach (int otherParent in ParentOf[childNode])
            {
                if (IsParentOf(parentNode, otherParent))
                {
                    return true;
                }
            }

            return false;
        }

        public void PopLastBlock()
        {
            if (_lastBlocks.Count > 1)
            {
                _ = _lastBlocks.Pop();
            }
        }

        private Edge LastEdge => Edges[_lastBlocks.Peek()];

        private readonly HashSet<int> _blocksWithGoto = new();

        public void MarkGotoOnBlock(int block, bool isExit)
        {
            if (isExit)
            {
                Blocks[block].Exit();
            }

            _ = _blocksWithGoto.Add(block);
        }

        public bool HasGoto(int block) => _blocksWithGoto.Contains(block);
    }
}