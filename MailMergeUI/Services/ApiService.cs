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
            await Task.Delay(800); // Simulate API
            return _rnd.Next(50, 500);
        }
    }
}
