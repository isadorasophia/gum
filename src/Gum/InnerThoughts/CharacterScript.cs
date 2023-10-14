using Newtonsoft.Json;

namespace Gum.InnerThoughts
{
    public class CharacterScript
    {
        /// <summary>
        /// List of tasks or events that the <see cref="Situations"/> may do.
        /// </summary>
        [JsonProperty]
        private readonly SortedList<int, Situation> _situations = new();

        private readonly Dictionary<string, int> _situationNames = new();

        [JsonProperty]
        private int _nextId = 0;

        public readonly string Name;

        public CharacterScript(string name) { Name = name; }

        private Situation? _currentSituation;

        public Situation CurrentSituation =>
            _currentSituation ?? throw new InvalidOperationException("☠️ Unable to fetch an active situation.");

        public bool HasCurrentSituation => _currentSituation != null;

        public bool AddNewSituation(ReadOnlySpan<char> name)
        {
            int id = _nextId++;

            string situationName = name.TrimStart().TrimEnd().ToString();
            if (_situationNames.ContainsKey(situationName))
            {
                return false;
            }

            _currentSituation = new Situation(id, situationName);

            _situations.Add(id, _currentSituation);
            _situationNames.Add(situationName, id);
            return true;
        }

        public Situation? FetchSituation(int id)
        {
            if (_situations.TryGetValue(id, out Situation? value))
            {
                return value;
            }

            return null;
        }

        public int? FetchSituationId(string name)
        {
            if (_situationNames.TryGetValue(name, out int id))
            {
                return id;
            }

            return null;
        }

        public string[] FetchAllNames() => _situationNames.Keys.ToArray();

        public IEnumerable<Situation> FetchAllSituations() => _situations.Values;
    }
}