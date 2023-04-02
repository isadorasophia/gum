using Newtonsoft.Json;
using System.Collections.Immutable;

namespace Whispers.InnerThoughts
{
    internal class CharacterScript
    {
        /// <summary>
        /// List of tasks or events that the <see cref="Situations"/> may do.
        /// </summary>
        [JsonProperty]
        private readonly SortedList<int, Situation> _situations = new();

        [JsonProperty]
        private int _nextId = 0;

        public CharacterScript() { }

        private Situation? _currentSituation;

        public Situation CurrentSituation => 
            _currentSituation ?? throw new InvalidOperationException("☠️ Unable to fetch an active situation.");

        public bool HasCurrentSituation => _currentSituation != null;
        
        public void AddNewSituation(ReadOnlySpan<char> name)
        {
            int id = _nextId++;

            _currentSituation = new Situation(id, name.ToString());
            _situations.Add(id, _currentSituation);
        }

        public Situation? FetchSituation(int id)
        {
            if (_situations.TryGetValue(id, out Situation? value))
            {
                return value;
            }

            return null;
        }
    }
}
