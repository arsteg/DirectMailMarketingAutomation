using MailMergeUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailMergeUI.Helpers
{
    public class DayCheckBoxViewModel : BaseViewModel
    {
        public DayOfWeek Day { get; }
        public string DisplayName => Day.ToString();
          

        private bool _isChecked;

        public bool IsChecked
        {
            get { return _isChecked; }
            set { _isChecked = value;
                OnPropertyChanged();
            }
        }

        public DayCheckBoxViewModel(DayOfWeek day, bool isChecked)
        {
            Day = day;
            IsChecked = isChecked;
        }
    }
}
