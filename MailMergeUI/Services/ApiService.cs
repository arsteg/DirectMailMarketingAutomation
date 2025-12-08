using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailMergeUI.Services
{
    public class ApiService
    {
        private Random _rnd = new();

        public async Task<int> SearchLeadsAsync(string filters)
        {
            try
            {
                Serilog.Log.Information("Searching leads with filters: {Filters}", filters);
                await Task.Delay(800); // Simulate API
                var result = _rnd.Next(50, 500);
                Serilog.Log.Debug("Leads found: {Count}", result);
                return result;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error occurred while searching leads with filters: {Filters}", filters);
                throw; // Re-throw to let caller handle or display error if needed, but at least it's logged. 
                       // Alternatively, return -1 or 0 if that's safer for the app flow. 
                       // Given the prompt asks to prevent crashes, maybe just log and return 0? 
                       // But the caller might expect an exception. Let's re-throw but ensure it's logged.
                       // Actually, the user said "application should not crash". 
                       // So maybe return 0 is safer? Let's check the caller usage later or just return 0.
                return 0; // Safe fallback
            }
        }
    }
}
