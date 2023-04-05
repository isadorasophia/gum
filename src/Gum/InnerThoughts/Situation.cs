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

        public readonly Dictionary<int, Relationship> BlocksRelationship = new();

        /// <summary>
        /// Track the relationships when adding those values.
        /// </summary>
        private readonly Stack<Relationship> _relationships  = new();

        private readonly Stack<int> _lastBlocks = new();

        public Situation() { }

        public Situation(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public bool SwitchRelationshipTo(RelationshipKind kind)
        {
            if (!_relationships.TryPeek(out Relationship? lastRelationship))
            {
                throw new InvalidOperationException("☠️ Error on the implementation! " +
                    "Why are we assuming a relationship exists here?");
            }
            
            if (lastRelationship.Kind == kind)
            {
                // No operation, relationship is already set.
                return true;
            }

            int length = lastRelationship.Blocks.Count;
            Debug.Assert(length != 0, "We expect that the last block added will be the block subjected to the " +
                "relationship switch.");

            Relationship? relationship;
            Block empty;

            switch (kind)
            {
                case RelationshipKind.Next:
                case RelationshipKind.Random:
                    lastRelationship.Kind = kind;
                    return true;

                case RelationshipKind.Choice:
                case RelationshipKind.HighestScore:
                    if (length == 1)
                    {
                        lastRelationship.Kind = kind;
                    }
                    else
                    {
                        Debug.Fail("I don't understand this scenario fully, please debug this.");
                        //int choiceBlock = lastRelationship.Blocks[length - 1];

                        //// "Grab" the first block, so it now points to the new relationship created.
                        //int previousBlock = lastRelationship.Blocks[length - 2];

                        //_ = lastRelationship.Blocks.Remove(choiceBlock);

                        //if (!BlocksRelationship.TryGetValue(previousBlock, out relationship))
                        //{
                        //    relationship = CreateRelationship(kind);
                        //    relationship.Blocks.Add(choiceBlock);

                        //    LinkRelationship(previousBlock, relationship);
                        //}
                        //else
                        //{
                        //    relationship.Blocks.Add(choiceBlock);
                        //    empty = CreateBlock(playUntil: ifBlock.PlayUntil, track: false);
                        //}
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
                        if (lastRelationship.Owners.Count > 2)
                        {
                            throw new InvalidOperationException("☠️ Error on the implementation! " +
                                "Somehow, you managed to get into a state where a 'else' relationship is grabbing a link from two different" +
                                "nodes. I wasn't sure if this was even possible. Could you report this?");
                        }

                        Block ifBlock = Blocks[lastRelationship.Owners[0]];
                        int elseBlock = lastRelationship.Blocks[0];

                        _ = _lastBlocks.Pop();
                        _ = _lastBlocks.Pop();

                        _lastBlocks.Push(elseBlock);

                        empty = CreateBlock(playUntil: ifBlock.PlayUntil, track: false);

                        _ = BlocksRelationship.Remove(ifBlock.Id);
                        _ = _relationships.Pop();

                        int ifBlockPosition = NextBlocks.IndexOf(ifBlock.Id);
                        NextBlocks[ifBlockPosition] = empty.Id;

                        relationship = CreateRelationship(kind);

                        relationship.Blocks.Add(ifBlock.Id);
                        relationship.Blocks.Add(elseBlock);

                        LinkRelationship(empty.Id, relationship);

                        // We found an else block without any prior block, other than ourselves!
                    }
                    else if (length == 2)
                    {
                        // Only if + else here, all good.
                        lastRelationship.Kind = kind;

                        int elseBlock = lastRelationship.Blocks[length - 1];

                        _ = _lastBlocks.Pop();
                        _ = _lastBlocks.Pop();

                        _lastBlocks.Push(elseBlock);
                    }
                    else
                    {
                        // We need to get surgical about this.
                        // "Remove" the last block and move it to a new relationship.
                        int ifBlock = lastRelationship.Blocks[length - 2];
                        int elseBlock = lastRelationship.Blocks[length - 1];

                        _ = lastRelationship.Blocks.Remove(ifBlock);
                        _ = lastRelationship.Blocks.Remove(elseBlock);

                        _ = _lastBlocks.Pop();
                        _ = _lastBlocks.Pop();

                        _lastBlocks.Push(elseBlock);

                        // "Grab" the first block, so it now points to the new relationship created.
                        int previousBlock = lastRelationship.Blocks[length - 3];

                        relationship = CreateRelationship(kind);

                        relationship.Blocks.Add(ifBlock);
                        relationship.Blocks.Add(elseBlock);

                        LinkRelationship(previousBlock, relationship);
                    }

                    return true;
            }

            return true;
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

            return block;
        }

        /// <summary>
        /// Creates a new block subjected to a <paramref name="kind"/> relationship.
        /// </summary>
        public Block AddBlock(int playUntil, bool join, bool isNested, RelationshipKind kind = RelationshipKind.Next)
        {
            // If this should actually join other nodes, make sure we do that.
            if (join && _relationships.Count > 0)
            {
                return CreateBlockWithJoin(playUntil);
            }

            // We need to know the "parent" node when nesting blocks (make the parent -> point to the new block).
            bool hasBlocks = _lastBlocks.TryPeek(out int lastBlockId);

            Relationship? relationship;
            Block block = CreateBlock(playUntil, track: true);

            // If this was called right after a situation has been declared, it'll think that it is a nested block
            // (when it's not really).
            
            if (isNested && hasBlocks)
            {
                // If there is a parent block that this can be nested to.
                if (hasBlocks)
                {
                    if (!BlocksRelationship.TryGetValue(lastBlockId, out relationship))
                    {
                        // The parent does not have an edge to other nodes. In that case, create one.
                        relationship = CreateRelationship(kind);
                        LinkRelationship(lastBlockId, relationship);
                    }

                    relationship.Blocks.Add(block.Id);
                }
                else
                {
                    relationship = CreateRelationship(RelationshipKind.Next);
                    relationship.Blocks.Add(block.Id);

                    LinkRelationship(lastBlockId, relationship);

                    if (relationship.Kind != RelationshipKind.Next)
                    {
                        relationship = CreateRelationship(kind);
                        LinkRelationship(block.Id, relationship);
                    }
                }
            }
            else if (!hasBlocks)
            {
                // This is actually a "root" node. Add it directly as the next available node and create a relationship.
                NextBlocks.Add(block.Id);

                relationship = CreateRelationship(kind);
                LinkRelationship(block.Id, relationship);

                // This is actually a non-sequential node, so it can't be simply tracked in "NextBlocks".
                // TODO: Remove NextBlocks?? I don't know why I added that.
                if (!kind.IsSequential())
                {
                    block = CreateBlock(playUntil, track: false);
                    relationship.Blocks.Add(block.Id);
                }
            }
            else
            {
                Relationship target = _relationships.Peek();

                if (kind == RelationshipKind.Choice && _lastBlocks.Count > 2)
                {
                    int parent = _lastBlocks.ElementAt(2);
                    if (BlocksRelationship.TryGetValue(parent, out relationship) && 
                        relationship.Kind == RelationshipKind.Choice)
                    {
                        target = relationship;
                    }
                }

                if (target.Kind != kind && target.Blocks.Count == 0 &&
                    NextBlocks.Contains(lastBlockId) && kind == RelationshipKind.Next)
                {
                    _relationships.Clear();
                    _lastBlocks.Clear();
                    _lastBlocks.Push(block.Id);

                    // This is actually a "root" node. Add it directly as the next available node and create a relationship.
                    NextBlocks.Add(block.Id);

                    relationship = CreateRelationship(kind);
                    LinkRelationship(block.Id, relationship);

                    // This is actually a non-sequential node, so it can't be simply tracked in "NextBlocks".
                    // TODO: Remove NextBlocks?? I don't know why I added that.
                    if (!kind.IsSequential())
                    {
                        block = CreateBlock(playUntil, track: false);
                        relationship.Blocks.Add(block.Id);
                    }

                    return block;
                }

                if (kind == RelationshipKind.IfElse && target.Kind != kind)
                {
                    if (target.Kind != RelationshipKind.Next)
                    {
                        _ = _lastBlocks.TryPop(out _);
                        _ = _lastBlocks.TryPop(out _);

                        _lastBlocks.Push(block.Id);

                        _ = _relationships.TryPop(out _);

                        Block empty = CreateBlock(playUntil: -1, track: false);

                        target = CreateRelationship(kind);

                        target.Blocks.Add(lastBlockId);
                        target.Blocks.Add(block.Id);

                        LinkRelationship(empty.Id, target);

                        int lastBlockPosition = NextBlocks.IndexOf(lastBlockId);
                        if (lastBlockPosition != -1)
                        {
                            NextBlocks[lastBlockPosition] = empty.Id;
                        }
                        else
                        {
                            Debug.Fail("Figure out whoever owns this.");
                        }

                        return block;
                    }

                    // This has been a bit of a headache, but if this is an "if else" and the current connection
                    // is not the same, we'll convert this later.
                    kind = RelationshipKind.Next;
                }

                if (kind == RelationshipKind.HighestScore && target.Kind == RelationshipKind.Random)
                {
                    // A "HighestScore" kind when is matched with a "random" relationship, it is considered one of them
                    // automatically.
                    kind = RelationshipKind.Random;
                }

                if (target.Kind != kind && target.Blocks.Count == 0)
                {
                    target.Kind = kind;
                }
                else if (target.Kind != kind && kind == RelationshipKind.HighestScore)
                {
                    target = CreateRelationship(kind);

                    LinkRelationship(lastBlockId, target);
                }
                else if (target.Kind != kind)
                {
                    _ = _relationships.Pop();

                    Block lastBlock = Blocks[lastBlockId];
                    Block empty = CreateBlock(playUntil: lastBlock.PlayUntil, track: false);

                    target = CreateRelationship(kind);
                    target.Blocks.Add(lastBlock.Id);

                    int lastBlockPosition = NextBlocks.IndexOf(lastBlockId);
                    if (lastBlockPosition != -1)
                    {
                        NextBlocks[lastBlockPosition] = empty.Id;
                    }
                    else
                    {
                        Debug.Fail("Figure out whoever owns this.");
                    }

                    LinkRelationship(empty.Id, target);
                }

                target.Blocks.Add(block.Id);
            }

            return block;
        }

        /// <summary>
        /// Find all leaf nodes eligible to be joined.
        /// This will disregard nodes that are already dead (due to a goto!).
        /// </summary>
        private void FindAllLeaves(int block, ref List<int> result)
        {
            if (BlocksRelationship.TryGetValue(block, out Relationship? relationship))
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
            Debug.Assert(_relationships.Count != 0, "This should only be called with a previous relationship.");

            bool hasAnyBlocks = _lastBlocks.Count != 0;

            // Get rid of the last blocks prior to the join, if any.
            if (hasAnyBlocks)
            {
                _ = _lastBlocks.TryPop(out _);
                _ = _lastBlocks.TryPop(out _);
            }

            Block joinedBlock = CreateBlock(playUntil, track: true);

            if (!hasAnyBlocks && _relationships.Count == 1)
            {
                NextBlocks.Add(joinedBlock.Id);
            }

            List<int> blocksThatWillBeJoined = _relationships.Peek().Blocks;
            List<int> leafBlocks = new();

            Relationship lastRelationship = _relationships.Pop();

            // Now, for each of those blocks, we'll collect all of its leaves and add edges to it.
            foreach (int blockToJoin in blocksThatWillBeJoined)
            {
                FindAllLeaves(blockToJoin, ref leafBlocks);
            }

            Relationship relationship;
            if (leafBlocks.Count == 0)
            {
                Debug.Assert(lastRelationship.Owners.Count == 1, "If this is not the case, consider refactoring.");

                _ = _lastBlocks.Pop();

                int owner = lastRelationship.Owners[0];
                Block empty = CreateBlock(playUntil: Blocks[owner].PlayUntil, track: true);

                _lastBlocks.Push(joinedBlock.Id);

                relationship = CreateRelationship(RelationshipKind.Next, stack: true);

                relationship.Blocks.Add(owner);
                relationship.Blocks.Add(joinedBlock.Id);

                LinkRelationship(empty.Id, relationship);

                int i = NextBlocks.IndexOf(owner);
                if (i != -1)
                {
                    NextBlocks[i] = empty.Id;
                }
                else
                {
                    Debug.Fail("If this is not the case, consider refactoring.");
                }
            }
            else
            {
                // Start by creating a link between previous blocks and this new one.
                relationship = CreateRelationship(RelationshipKind.Next, stack: false);
                relationship.Blocks.Add(joinedBlock.Id);

                foreach (int i in leafBlocks)
                {
                    LinkRelationship(i, relationship);
                }
            }

            // Finally, create a new relationship for the block itself.
            relationship = CreateRelationship(RelationshipKind.Next);
            LinkRelationship(joinedBlock.Id, relationship);

            return joinedBlock;
        }

        private Relationship CreateRelationship(RelationshipKind kind, bool stack = true)
        {
            Relationship relationship = new(kind);

            if (stack)
            {
                _relationships.Push(relationship);
            }

            return relationship;
        }

        private void LinkRelationship(int id, Relationship relationship)
        {
            BlocksRelationship.Add(id, relationship);
            relationship.Owners.Add(id);
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
            if (!_relationships.TryPeek(out Relationship? relationship))
            {
                // No op.
                return;
            }

            if (relationship.Blocks.Count == 0)
            {
                _ = _relationships.Pop();
                _ = _lastBlocks.Pop();
            }
            else
            {
                foreach (int i in relationship.Blocks)
                {
                    _ = _lastBlocks.TryPop(out _);
                }

                if (relationship.Kind == RelationshipKind.Random)
                {
                    _ = _relationships.Pop();
                    _ = _lastBlocks.Pop();
                }
                else if (_relationships.Count >= 2 && _relationships.ElementAt(1).Kind == RelationshipKind.Choice)
                {
                    _ = _relationships.Pop();
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
