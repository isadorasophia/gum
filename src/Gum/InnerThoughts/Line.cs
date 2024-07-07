﻿using System.Diagnostics;

namespace Gum.InnerThoughts
{
    [DebuggerDisplay("{Text}")]
    public readonly struct Line
    {
        /// <summary>
        /// This may be the speaker name or "Owner" for whoever owns this script.
        /// </summary>
        public readonly string? Speaker;

        public readonly string? Portrait = null;

        /// <summary>
        /// If the caption has a text, this will be the information.
        /// </summary>
        public readonly string? Text = null;

        /// <summary>
        /// Delay in seconds.
        /// </summary>
        public readonly float? Delay = null;

        public Line() { }

        public Line(string? speaker) => Speaker = speaker;

        /// <summary>
        /// Create a line with a text. That won't be used as a timer.
        /// </summary>
        public Line(string? speaker, string? portrait, string text) => 
            (Speaker, Portrait, Text) = (speaker, portrait, text);

        public bool IsText => Text is not null;
    }
}