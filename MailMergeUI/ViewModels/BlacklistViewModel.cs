using MailMerge.Data;
using MailMerge.Data.Models;
using MailMergeUI.Dialogs;
using MailMergeUI.Helpers;   // <-- make sure this namespace is included
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MailMergeUI.ViewModels
{
    public class BlackListViewModel : BaseViewModel
    {
        private readonly MailMergeDbContext _dbContext;

        // ───── Collections ─────
        private ObservableCollection<PropertyRecord> _searchResults = new();
        public ObservableCollection<PropertyRecord> SearchResults
        {
            get => _searchResults;
            set { _searchResults = value; OnPropertyChanged(); }
        }

        private ObservableCollection<BlacklistedProperty> _blacklist = new();
        public ObservableCollection<BlacklistedProperty> Blacklist
        {
            get => _blacklist;
            set { _blacklist = value; OnPropertyChanged(); }
        }

        // ───── Search Input ─────
        private string _searchAddress = "";
        public string SearchAddress
        {
            get => _searchAddress;
            set
            {
                _searchAddress = value;
                OnPropertyChanged();
                SearchCommand.RaiseCanExecuteChanged();
            }
        }

        // ───── Commands ─────
        public RelayCommand SearchCommand { get; }
        public RelayCommand<PropertyRecord> ToggleBlacklistCommand { get; }   // generic!

        // ───── Constructor ─────
        public BlackListViewModel(MailMergeDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

            SearchCommand = new RelayCommand(_ => PerformSearch(), _ => CanSearch());
            ToggleBlacklistCommand = new RelayCommand<PropertyRecord>(ToggleBlacklist);

            LoadBlacklist();
        }

        private bool CanSearch() => !string.IsNullOrWhiteSpace(SearchAddress);

        // ───── Search (async wrapper) ─────
        private void PerformSearch()
        {
            Task.Run(async () => await PerformSearchAsync()).ConfigureAwait(false);
        }

        private async Task PerformSearchAsync()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SearchResults.Clear();
            });
           
           
            if (string.IsNullOrWhiteSpace(SearchAddress)) return;

            var term = SearchAddress.Trim().ToLowerInvariant();

            var results = await _dbContext.Properties
                .AsNoTracking()
                .Where(p =>
                    (p.Address != null && p.Address.ToLower().Contains(term)) ||
                    (p.RadarId != null && p.RadarId.ToLower().Contains(term)) ||
                    (p.Apn != null && p.Apn.ToLower().Contains(term)) ||
                    (p.Owner != null && p.Owner.ToLower().Contains(term)))
                .Take(100)
                .ToListAsync();

            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var r in results) SearchResults.Add(r);
            });
        }

        // ───── Load Blacklist History ─────
        private void LoadBlacklist()
        {
            Task.Run(async () =>
            {
                var blacklisted = await _dbContext.Properties
                    .AsNoTracking()
                    .Where(p => p.IsBlackListed)
                    .OrderByDescending(p => p.BlackListedOn)
                    .Take(50)
                    .Select(p => new BlacklistedProperty
                    {
                        Address = p.Address ?? "N/A",
                        BlacklistedAt = p.BlackListedOn ?? DateTime.MinValue,
                        BlackListingReason = p.BlackListingReason
                    })
                    .ToListAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Blacklist.Clear();
                    foreach (var b in blacklisted) Blacklist.Add(b);
                });
            }).ConfigureAwait(false);
        }

        // ───── Toggle Blacklist (called from checkbox) ─────
        private void ToggleBlacklist(PropertyRecord? property)
        {
            if (property == null) return;

            if (property.IsBlackListed)
            {
                // ---- BLACKLISTING ----
                var dialog = new ReasonInputDialog
                {
                    Owner = Application.Current.MainWindow,
                    Reason = property.BlackListingReason ?? ""
                };

                if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Reason))
                {
                    property.IsBlackListed = false;   // revert UI
                    return;
                }

                property.BlackListingReason = dialog.Reason.Trim();
                property.BlackListedOn = DateTime.UtcNow;

                _dbContext.Update(property);
                _dbContext.SaveChanges();

                // Add to history
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var display = new BlacklistedProperty
                    {
                        Address = property.Address ?? "Unknown",
                        BlacklistedAt = property.BlackListedOn ?? DateTime.Now,
                        BlackListingReason = property.BlackListingReason
                    };
                    Blacklist.Insert(0, display);
                });

                MessageBox.Show(
                    $"Property blacklisted.\nAddress: {property.Address}\nReason: {dialog.Reason}",
                    "Blacklisted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // ---- UN-BLACKLISTING ----
                var result = MessageBox.Show(
                    $"Remove blacklist from:\n{property.Address}?",
                    "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    property.IsBlackListed = true;   // revert UI
                    return;
                }

                property.IsBlackListed = false;
                property.BlackListingReason = null;
                property.BlackListedOn = null;

                _dbContext.Update(property);
                _dbContext.SaveChanges();

                // Remove from history
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var toRemove = Blacklist.FirstOrDefault(b => b.Address == property.Address);
                    if (toRemove != null) Blacklist.Remove(toRemove);
                });

                MessageBox.Show("Property removed from blacklist.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    // ───── Display model for history ─────
    public class BlacklistedProperty
    {
        public string Address { get; set; } = "";
        public DateTime BlacklistedAt { get; set; }
        public string? BlackListingReason { get; set; }
    }
}