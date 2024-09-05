using Gum.InnerThoughts;
using Gum.Utilities;

namespace Gum
{
    public partial class Parser
    {
        private bool ResolveAllGoto()
        {
            foreach ((Block block, string location, int line) in _gotoDestinations)
            {
                if (!_script.CanFetchSituationId(location))
                {
                    OutputHelpers.WriteError($"Unable to find a situation of name '{location}' reference in line {line}.");
                    if (TryFindABestGuess(location) is string guess)
                    {
                        OutputHelpers.ProposeFix(line, before: $"-> {location}", after: $"-> {guess}");
                    }

                    return false;
                }

                if (block.GoTo is not null)
                {
                    OutputHelpers.WriteWarning($"Additional goto statement found '-> {location}' in {line}. It will be disregarded, was that intentional?");
                    continue;
                }

                block.GoTo = location;
            }

            return true;
        }

        /// <summary>
        /// Try to guess the best name of a location that the person may have meant.
        /// This is only used for diagnostics.
        /// </summary>
        private string? TryFindABestGuess(string guess)
        {
            string[] locations = _script.FetchAllNames();

            int minInsersect = (int)Math.Round(guess.Length * .55f);

            int bestMatchCount = 0;
            string? bestMatch = null;

            foreach (string location in locations)
            {
                int intersectCount = guess.Intersect(location).Count();

                // Did it succeed the bare minimum?
                if (intersectCount > minInsersect)
                {
                    // Did it beat our best match yet?
                    if (intersectCount > bestMatchCount)
                    {
                        bestMatchCount = intersectCount;
                        bestMatch = location;
                    }
                }
            }

            return bestMatch;
        }
    }
}