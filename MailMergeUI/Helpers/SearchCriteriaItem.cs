using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailMergeUI.Helpers
{
    public class SearchCriteriaItem
    {
        public string Name { get; set; }
        public List<string> Value { get; set; }

        public SearchCriteriaItem(string name, params string[] values)
        {
            Name = name;
            if(values == null || values.Length == 0)
                Value = new List<string>();
            else
                Value = new List<string>(values);
        }
    }

    public class SearchCriteriaBody
    {
        public List<SearchCriteriaItem> Criteria { get; set; } = new();
    }

    public static class SearchCriteriaHelper
    {
        public static string BuildSearchCriteriaJson(string State, string City)
        {
            try
            {
                var criteriaBody = new SearchCriteriaBody
                {
                    Criteria = new List<SearchCriteriaItem>
                {
                    new SearchCriteriaItem("State", State ?? string.Empty),
                    new SearchCriteriaItem("City", City ?? string.Empty),
                    new SearchCriteriaItem("inForeclosure", "1"),
                    new SearchCriteriaItem("ForeclosureStage", "Preforeclosure", "Auction"),
                    new SearchCriteriaItem("ForeclosureRecDate", "Last Week")
                }
                };

                // Return JSON string
                return JsonConvert.SerializeObject(criteriaBody, Formatting.Indented);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error building JSON: {ex.Message}");
                return string.Empty;
            }
        }
        /// <summary>
        /// Deserializes JSON and extracts the State and City values.
        /// </summary>
        public static (string State, string City) GetStateAndCityFromJson(string filtersJson)
        {
            if (string.IsNullOrWhiteSpace(filtersJson))
                return (string.Empty, string.Empty);

            try
            {
                var criteriaBody = JsonConvert.DeserializeObject<SearchCriteriaBody>(filtersJson);

                var stateItem = criteriaBody?.Criteria?.FirstOrDefault(c =>
                    c.Name.Equals("State", StringComparison.OrdinalIgnoreCase));
                var cityItem = criteriaBody?.Criteria?.FirstOrDefault(c =>
                    c.Name.Equals("City", StringComparison.OrdinalIgnoreCase));

                string state = stateItem?.Value?.FirstOrDefault() ?? string.Empty;
                string city = cityItem?.Value?.FirstOrDefault() ?? string.Empty;

                return (state, city);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing JSON: {ex.Message}");
                return (string.Empty, string.Empty);
            }
        }
    }

}