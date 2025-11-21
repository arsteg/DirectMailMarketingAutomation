using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MailMergeUI.Helpers
{
    public class LocationValidator
    {
        private static Dictionary<string, List<string>> StatesAndCities;

        static LocationValidator()
        {
            LoadStateCityData("data/us_states_cities.json");
        }

        private static void LoadStateCityData(string jsonPath)
        {
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException($"Data file not found: {jsonPath}");

            string json = File.ReadAllText(jsonPath);
            StatesAndCities = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        public static (bool IsValid, string Message) ValidateLocation(string stateName, string cityNames = null)
        {
            // Validate state
            if (string.IsNullOrWhiteSpace(stateName))
                return (false, "Please provide a state name.");

            stateName = stateName.Trim();

            if (!StatesAndCities.ContainsKey(stateName))
                return (false, $"Invalid state name: '{stateName}'.");

            // No cities specified → state is valid, we're done
            if (string.IsNullOrWhiteSpace(cityNames))
                return (true, $"Valid state '{stateName}' provided.");

            // Cities were specified → split and validate ALL of them
            var cities = cityNames.Split(',')
                                  .Select(c => c.Trim())
                                  .Where(c => !string.IsNullOrEmpty(c))
                                  .ToList();

            // In case someone passes only commas/spaces like ", ,"
            if (!cities.Any())
                return (true, $"Valid state '{stateName}' provided (no cities specified).");

            var stateCities = StatesAndCities[stateName];
            var invalidCities = new List<string>();

            foreach (var city in cities)
            {
                if (!stateCities.Any(c => c.Equals(city, StringComparison.OrdinalIgnoreCase)))
                    invalidCities.Add(city);
            }

            // All cities are valid
            if (invalidCities.Count == 0)
            {
                string cityList = cities.Count == 1
                    ? $"'{cities[0]}'"
                    : $"({string.Join(", ", cities)})";

                return (true, cities.Count == 1
                    ? $"Valid city {cityList} found in '{stateName}'."
                    : $"All specified cities {cityList} are valid in '{stateName}'.");
            }

            // Some or all cities are invalid
            string invalidList = string.Join(", ", invalidCities);
            return (false, invalidCities.Count == 1
                ? $"Invalid city '{invalidList}' for state '{stateName}'."
                : $"The following cities do not exist in '{stateName}': {invalidList}.");
        }

    }
}
