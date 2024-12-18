﻿using System;
using System.Diagnostics;
using System.Text;

namespace Gum.InnerThoughts
{
    [DebuggerDisplay("{DebuggerDisplay(),nq}")]
    public class Block
    {
        public readonly int Id = 0;

        /// <summary>
        /// Stop playing this dialog until this number.
        /// If -1, this will play forever.
        /// </summary>
        public int PlayUntil = -1;

        /// <summary>
        /// Chance of executing this dialogue. This ranges from 0 to 1.
        /// </summary>
        public float Chance = 1;

        public readonly List<CriterionNode> Requirements = new();

        public readonly List<Line> Lines = new();

        public List<DialogAction>? Actions = null;

        /// <summary>
        /// Go to another dialog with a specified id.
        /// </summary>
        public string? GoTo = null;

        public bool NonLinearNode = false;

        public bool IsChoice = false;

        public bool IsExit = false;

        public bool Conditional = false;

        public bool CanBeSkipped => Requirements.Count > 0 || PlayUntil != -1 || Chance != 1;

        public Block() { }

        public Block(int id, int playUntil, float chance) => 
            (Id, PlayUntil, Chance) = (id, playUntil, chance);

        public void AddLine(string? speaker, string? portrait, string text)
        {
            Lines.Add(new(speaker, portrait, text));
        }

        public void AddRequirement(CriterionNode node)
        {
            Requirements.Add(node);
        }

        public void AddAction(DialogAction action)
        {
            Actions ??= new();
            Actions.Add(action);
        }

        public void Exit()
        {
            IsExit = true;
        }

        public string DebuggerDisplay()
        {
            StringBuilder result = new();
            _ = result.Append(
                $"[{Id}, Requirements = {Requirements.Count}, Lines = {Lines.Count}, Actions = {Actions?.Count ?? 0}]");

            return result.ToString();
        }
    }
}