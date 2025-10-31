using MailMergeUI.Helpers;
using MailMergeUI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MailMergeUI.ViewModels
{
    public class SystemLogViewModel : BaseViewModel
    {
        private readonly LogService _log = new();
        private string _logContent;

        public string LogContent
        {
            get => _logContent;
            set { _logContent = value; OnPropertyChanged(); }
        }

        public ICommand RefreshLogCommand { get; }

        public SystemLogViewModel()
        {
            RefreshLogCommand = new RelayCommand(_ => LoadLog());
            LoadLog();
        }

        private void LoadLog()
        {
            LogContent = _log.ReadLog();
        }
    }
}
