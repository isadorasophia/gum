using System;

namespace Whispers.Utilities
{
    internal static class OutputHelpers
    {
        public static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"🤚 Error! {message}");
            Console.ResetColor();
        }

        public static void WriteWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"🚧 Warning! {message}");
            Console.ResetColor();
        }

        public static void Suggestion(string message)
        {
            Console.WriteLine($"💡 Did you mean {message}");
        }

        public static void Remark(string message)
        {
            Console.WriteLine(message);
        }

        public static void ProposeFix(int line, ReadOnlySpan<char> before, ReadOnlySpan<char> after)
        {
            Console.WriteLine("Suggestion:");
            Console.WriteLine($"\tLine {line} | {before}");
            Console.WriteLine($"\t   ⬇️");
            Console.WriteLine($"\tLine {line} | {after}");
        }

        public static void ProposeFixAtColumn(int line, int column, int arrowLength, ReadOnlySpan<char> content, ReadOnlySpan<char> issue)
        {
            Console.WriteLine($"\tLine {line} | {content}");

            Console.Write("\t          ");

            while (column-- > 0)
            {
                Console.Write(' ');
            }

            // The emoji occupies sort of two characters?
            arrowLength = Math.Max(1, arrowLength/2);

            while (arrowLength-- > 0)
            {
                Console.Write($"☝️");
            }
            Console.Write($" {issue}");
        }
    }
}
