using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Whispers
{
    /// <summary>
    /// This is the parser entrypoint when converting .whispers -> metadata.
    /// </summary>
    internal class Entrypoint
    {
        /// <param name="args">
        /// This expects the following arguments:
        ///   `.\Whispers <input-path> <output-path>`
        ///   <input-path> can be the path of a directory or a single file.
        ///   <output-path> is where the output should be created.
        /// </param>
        internal static void Main(string[] arguments)
        {
            // TODO: Check and debug this.
            // If this got called from the msbuild command, for example,
            // it might group different arguments into the same string.
            // We manually split them if there's such a case.
            // Regex stringParser = new Regex(@"([^""]+)""|\s*([^\""\s]+)");

            Console.OutputEncoding = Encoding.UTF8;

            if (arguments.Length != 2)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("This expects the following arguments:\n" +
                    "\t.\\Whispers <input-path> <output-path>\n\n" +
                    "\t  - <input-path> can be the path of a directory or a single file.\n" +
                    "\t  - <output-path> is where the output should be created.\n");
                Console.ResetColor();

                throw new ArgumentException(nameof(arguments));
            }

            // Handles any relative path.
            string ToRootPath(string s) =>
                Path.IsPathRooted(s) ? s : Path.GetFullPath(Path.Join(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location), s));

            string inputPath = ToRootPath(arguments[0]);
            string outputPath = ToRootPath(arguments[1]);

            IEnumerable<string> files = GetAllLibrariesInPath(inputPath);
            foreach (string file in files)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"✨ Compiling {Path.GetFileName(file)}...");
                Console.ResetColor();

                _ = Parser.Parse(file);
            }
        }

        /// <summary>
        /// Look recursively for all the files in <paramref name="path"/>.
        /// </summary>
        /// <param name="path">Rooted path to the binaries folder. This must be a valid directory.</param>
        private static IEnumerable<string> GetAllLibrariesInPath(in string path)
        {
            if (File.Exists(path))
            {
                return new string[] { path };
            }

            // 1. Filter all files that has a "*.whispers" extension.
            // 3. Distinguish the file names.
            return Directory.EnumerateFiles(path, "*.whispers", SearchOption.AllDirectories)
                .GroupBy(s => Path.GetFileName(s))
                .Select(s => s.First());
        }
    }
}