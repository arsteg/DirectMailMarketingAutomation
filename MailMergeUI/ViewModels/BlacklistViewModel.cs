using MailMergeUI.Helpers;
using MailMergeUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MailMergeUI.ViewModels
{
    public class BlacklistViewModel : BaseViewModel
    {
        public ObservableCollection<BlacklistedProperty> Blacklist { get; } = new();

        private string _searchAddress = "";
        public string SearchAddress
        {
            get => _searchAddress;
            set { _searchAddress = value; OnPropertyChanged(); }
        }

        public ICommand BlacklistCommand { get; }

        public BlacklistViewModel()
        {
            BlacklistCommand = new RelayCommand(_ => AddToBlacklist(), _ => !string.IsNullOrWhiteSpace(SearchAddress));
            LoadSampleBlacklist();
        }

        private void LoadSampleBlacklist()
        {
            Blacklist.Add(new BlacklistedProperty { Address = "123 Main St, City", BlacklistedAt = DateTime.Today.AddDays(-5) });
        }

        private void AddToBlacklist()
        {
            if (Blacklist.Any(b => b.Address.Equals(SearchAddress, System.StringComparison.OrdinalIgnoreCase)))
                return;

            Blacklist.Add(new BlacklistedProperty { Address = SearchAddress });
            SearchAddress = "";
        }
    }
}
