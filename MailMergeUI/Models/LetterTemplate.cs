using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailMergeUI.Models
{
    // Models/LetterTemplate.cs (for Template Library)
    public class LetterTemplate
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Untitled Template";
        public string FilePath { get; set; } = ""; // Full path to .docx
    }
}
