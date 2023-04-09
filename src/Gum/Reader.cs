using Gum.InnerThoughts;
using Gum.Utilities;
using Murder.Serialization;
using Newtonsoft.Json;
using System.Reflection;
using System.Text;

namespace Gum
{
    /// <summary>
    /// This is the parser entrypoint when converting .gum -> metadata.
    /// </summary>
    public class Reader
    {
        /// <param name="arguments">
        /// This expects the following arguments:
        ///   `.\Gum <input-path> <output-path>`
        ///   <input-path> can be the path of a directory or a single file.
        ///   <output-path> is where the output should be created.
        /// </param>
        internal static void Main(string[] arguments)
        {
            // If this got called from the msbuild command, for example,
            // it might group different arguments into the same string.
            // We manually split them if there's such a case.
            //
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

            string outputPath = ToRootPath(arguments[1]);
            if (!Directory.Exists(outputPath))
            {
                OutputHelpers.WriteError($"Unable to find output path '{outputPath}'");
                return;
            }

            CharacterScript[] scripts = ParseImplementation(inputPath: arguments[0], lastModified: null, DiagnosticLevel.All);

            foreach (CharacterScript script in scripts)
            {
                _ = Save(script, outputPath);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"🪄 Success! Successfully saved output at {outputPath}.");
            Console.ResetColor();
        }

        internal static bool Save(CharacterScript script, string path)
        {
            string json = JsonConvert.SerializeObject(script, Settings);
            File.WriteAllText(path: Path.Join(path, $"{script.Name}.json"), contents: json);

            return true;
        }

        internal static CharacterScript? Retrieve(string filepath)
        {
            if (!File.Exists(filepath))
            {
                OutputHelpers.WriteError($"Unable to find file at '{filepath}'");
                return null;
            }

            string json = File.ReadAllText(filepath);
            return JsonConvert.DeserializeObject<CharacterScript>(json);
        }

        /// <summary>
        /// This will parse all the documents in <paramref name="inputPath"/>.
        /// </summary>
        public static CharacterScript[] Parse(string inputPath, DateTime? lastModified, out string errors)
        {
            StringWriter writer = new();

            Console.SetOut(writer);

            CharacterScript[] result = ParseImplementation(inputPath, lastModified, DiagnosticLevel.ErrorsOnly);
            errors = writer.ToString();

            return result;
        }

        /// <summary>
        /// This will parse all the documents in <paramref name="inputPath"/>.
        /// </summary>
        private static CharacterScript[] ParseImplementation(string inputPath, DateTime? lastModified, DiagnosticLevel level)
        {
            OutputHelpers.Level = level;

            inputPath = ToRootPath(inputPath);

            List<CharacterScript> scripts = new List<CharacterScript>();

            IEnumerable<string> files = GetAllLibrariesInPath(inputPath, lastModified);
            foreach (string file in files)
            {
                OutputHelpers.Log($"✨ Compiling {Path.GetFileName(file)}...");

                CharacterScript? script = Parser.Parse(file);
                if (script is not null)
                {
                    scripts.Add(script);
                }
            }

            return scripts.ToArray();
        }

        internal readonly static JsonSerializerSettings Settings = new()
        {
            TypeNameHandling = TypeNameHandling.None,
            ContractResolver = new WritablePropertiesOnlyResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// Handles any relative path to the executable.
        /// </summary>
        private static string ToRootPath(string s) =>
            Path.IsPathRooted(s) ? s : Path.GetFullPath(Path.Join(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location), s));

        /// <summary>
        /// Look recursively for all the files in <paramref name="path"/>.
        /// </summary>
        /// <param name="path">Rooted path to the binaries folder. This must be a valid directory.</param>
        private static IEnumerable<string> GetAllLibrariesInPath(in string path, DateTime? lastModified)
        {
            if (File.Exists(path))
            {
                return new string[] { path };
            }

            if (!Path.Exists(path))
            {
                OutputHelpers.WriteError($"Unable to find input path '{path}'");
                return new string[0];
            }

            // 1. Filter all files that has a "*.gum" extension.
            // 2. Distinguish the file names.
            IEnumerable<string> paths = Directory.EnumerateFiles(path, "*.gum", SearchOption.AllDirectories)
                .GroupBy(s => Path.GetFileName(s))
                .Select(s => s.First());

            if (lastModified is not null)
            {
                // Only select files that have been modifier prior to a specific date.
                paths = paths.Where(s => File.GetLastWriteTime(s) > lastModified);
            }

            return paths;
        }
    }
}