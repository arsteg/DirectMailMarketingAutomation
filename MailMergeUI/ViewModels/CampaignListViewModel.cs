using MailMerge.Data;
using MailMerge.Data.Models;
using MailMergeUI.Helpers;
using MailMergeUI.Models;
using MailMergeUI.Services;
using MailMergeUI.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Serilog;

namespace MailMergeUI.ViewModels
{
    public class CampaignListViewModel : BaseViewModel
    {
        private readonly CampaignService _service;
        private readonly MailMergeDbContext _dbContext;
        public string PageInfoText => $"Page {CurrentPage} of {TotalPages}";


        public ObservableCollection<Campaign> Campaigns { get; } = new();

        // Pagination properties
        private int _currentPage = 1;
        private int _itemsPerPage = 25;
        private int _totalPages;

        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage != value)
                {
                    _currentPage = value;
                    OnPropertyChanged();
                    LoadCampaigns();
                }
            }
        }

        public int ItemsPerPage
        {
            get => _itemsPerPage;
            set
            {
                if (_itemsPerPage != value)
                {
                    _itemsPerPage = value;
                    OnPropertyChanged();
                    LoadCampaigns();
                }
            }
        }

        public int TotalPages
        {
            get => _totalPages;
            private set
            {
                _totalPages = value;
                OnPropertyChanged();
            }
        }

        // Commands
        public ICommand NewCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand PrevPageCommand { get; }

        public CampaignListViewModel(MailMergeDbContext dbContext)
        {
            _dbContext = dbContext;
            _service = new CampaignService(_dbContext);

            // Initialize commands
            NewCommand = new RelayCommand(_ => OpenEdit(null));
            EditCommand = new RelayCommand(c => OpenEdit((Campaign)c!), c => c is not null);
            DeleteCommand = new RelayCommand(c => Delete((Campaign)c!), c => c is not null);
            NextPageCommand = new RelayCommand(_ => GoToNextPage(), _ => CurrentPage < TotalPages);
            PrevPageCommand = new RelayCommand(_ => GoToPrevPage(), _ => CurrentPage > 1);

            LoadCampaigns();
        }

        private void LoadCampaigns()
        {
            if (_service == null)
                return;

            Campaigns.Clear();

            var allCampaigns = _service.Campaigns.ToList();
            TotalPages = (int)Math.Ceiling((double)allCampaigns.Count / ItemsPerPage);

            var pagedCampaigns = allCampaigns
                .Skip((CurrentPage - 1) * ItemsPerPage)
                .Take(ItemsPerPage)
                .ToList();

            foreach (var c in pagedCampaigns)
                Campaigns.Add(c);

            TotalPages = (int)Math.Ceiling((double)allCampaigns.Count / ItemsPerPage);
            if (CurrentPage > TotalPages)
                CurrentPage = TotalPages;

            OnPropertyChanged(nameof(Campaigns));
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(TotalPages));

            OnPropertyChanged(nameof(PageInfoText));
            OnPropertyChanged(nameof(CurrentPage));            
            OnPropertyChanged(nameof(allCampaigns.Count)); 

        }

        private void OpenEdit(Campaign? campaign)
        {
            var vm = new CampaignEditViewModel(campaign, _service,_dbContext);   // 2 parameters only
            vm.OnSaved += () => LoadCampaigns();                     // parameter-less
            var window = new CampaignEditWindow(vm);
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null)
            {
                window.WindowState = mainWindow.WindowState;

                // Optional: position on top of main window
                window.Owner = mainWindow;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            window.Show();
        }

        private void Delete(Campaign campaign)
        {
            if (MessageBox.Show($"Delete campaign '{campaign.Name}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _service.Campaigns.Remove(campaign);
                _service.DeleteCampaign(campaign);
                LoadCampaigns();
            }
        }

        private void GoToNextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                LoadCampaigns();
            }
        }

        private void GoToPrevPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                LoadCampaigns();
            }
        }       
    }
}
