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

        public static (bool, string) ValidateLocation(string stateName, string cityName = null)
        {
            if (string.IsNullOrWhiteSpace(stateName))
                return (false,"Please provide a state name.");

            if (!StatesAndCities.ContainsKey(stateName))
                return (false, $"Invalid state name: '{stateName}'.");

            if (!string.IsNullOrWhiteSpace(cityName))
            {
                bool cityExists = StatesAndCities[stateName]
               .Any(c => c.Equals(cityName, StringComparison.OrdinalIgnoreCase));
                
                return (cityExists, cityExists
                ? $"Valid city '{cityName}' found in '{stateName}'."
                : $"Invalid city '{cityName}' for state '{stateName}'.");
            }
            else {
                return (true, $"Valid state '{stateName}' provided.");
            }
        }
    }
}
