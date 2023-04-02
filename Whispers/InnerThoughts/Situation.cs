using System.Diagnostics;

namespace Whispers.InnerThoughts
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

            switch (kind)
            {
                case RelationshipKind.Next:
                case RelationshipKind.HighestScore:
                case RelationshipKind.Random:
                    lastRelationship.Kind = kind;
                    return true;

                case RelationshipKind.IfElse:
                    Debug.Assert(lastRelationship.Kind == RelationshipKind.Next, 
                        "We shouldn't be able to reach here if the relationship is not 'Next'.");

                    // This assumes that the 'else' block has already been inserted into the relationship
                    // and corresponds to the last added block.

                    int length = lastRelationship.Blocks.Count;

                    // First, rearrange our current expectations.
                    if (length <= 1)
                    {
                        // We found an else block without any prior block, other than ourselves!
                        return false;
                    }
                    else if (length == 2)
                    {
                        // Only if + else here, all good.
                        lastRelationship.Kind = kind;
                        return true;
                    }
                    else
                    {
                        // We need to get surgical about this.
                        // "Remove" the last block and move it to a new relationship.
                        int ifBlock = lastRelationship.Blocks[length - 2];
                        int elseBlock = lastRelationship.Blocks[length - 1];

                        _ = lastRelationship.Blocks.Remove(ifBlock);
                        _ = lastRelationship.Blocks.Remove(elseBlock);

                        // "Grab" the first block, so it now points to the new relationship created.
                        int lastBlock = lastRelationship.Blocks[length - 3];

                        Relationship relationship = new(kind);

                        relationship.Blocks.Add(ifBlock);
                        relationship.Blocks.Add(elseBlock);

                        BlocksRelationship.Add(lastBlock, relationship);
                    }

                    return true;
            }

            return true;
        }

        /// <summary>
        /// Creates a new block and assign an id to it.
        /// </summary>
        private Block CreateBlock(int playUntil)
        {
            int id = Blocks.Count;
            Block block = new(id, playUntil);

            Blocks.Add(block);

            return block;
        }

        /// <summary>
        /// Creates a block by joining whatever blocks were in the last relationship and point to it.
        /// </summary>
        private Block CreateBlockWithJoin(int playUntil)
        {
            Debug.Assert(_relationships.Count != 0, "This should only be called with a previous relationship.");

            Block joinedBlock = CreateBlock(playUntil);

            List<int> blocksThatWillBeJoined = _relationships.Peek().Blocks;

            // Start by creating a link between previous blocks and this new one.
            Relationship relationship = new(RelationshipKind.Next);
            relationship.Blocks.Add(joinedBlock.Id);

            _relationships.Push(relationship);

            foreach (int i in blocksThatWillBeJoined)
            {
                BlocksRelationship.Add(i, relationship);
            }

            // Finally, create a relationship for the block itself.
            relationship = new(RelationshipKind.Next);
            BlocksRelationship.Add(joinedBlock.Id, relationship);

            return joinedBlock;
        }

        /// <summary>
        /// Creates a new empty with a set number of occurrences.
        /// </summary>
        public Block AddChildBlock(int playUntil, bool join)
        {
            if (join || _relationships.Count == 0)
            {
                // This is actually the first block here, so create a new block instead.
                return CreateBlock(playUntil, join, RelationshipKind.Next);
            }

            Block block = CreateBlock(playUntil);

            _relationships.Peek().Blocks.Add(block.Id);
            return block;
        }

        /// <summary>
        /// Creates a new empty with a set number of occurrences.
        /// </summary>
        public Block CreateBlock(int playUntil, bool join, RelationshipKind kind)
        {
            if (join && _relationships.Count > 0)
            {
                return CreateBlockWithJoin(playUntil);
            }

            // Create the empty block.
            Block block = CreateBlock(playUntil);

            NextBlocks.Add(block.Id);

            Relationship relationship = new(kind);

            _relationships.Push(relationship);
            BlocksRelationship.Add(block.Id, relationship);

            return block;
        }
    }
}
