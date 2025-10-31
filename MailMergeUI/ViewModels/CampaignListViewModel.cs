using MailMerge.Data;
using MailMerge.Data.Models;
using MailMergeUI.Helpers;
using MailMergeUI.Models;
using MailMergeUI.Services;
using MailMergeUI.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MailMergeUI.ViewModels
{
    public class CampaignListViewModel : BaseViewModel
    {
        private readonly CampaignService _service;
        public ObservableCollection<Campaign> Campaigns { get; } = new();

        public ICommand NewCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        private readonly MailMergeDbContext _dbContext;

        public CampaignListViewModel(MailMergeDbContext dbContext)
        {
            _dbContext = dbContext;
            _service = new CampaignService(_dbContext);
            RefreshCampaigns();

            NewCommand = new RelayCommand(_ => OpenEdit(null));
            EditCommand = new RelayCommand(c => OpenEdit((Campaign)c!));
            DeleteCommand = new RelayCommand(c => Delete((Campaign)c!), c => c is not null);
        }

        private void RefreshCampaigns()
        {
            if (_service == null)
                return;
            Campaigns.Clear();
            foreach (var c in _service?.Campaigns) Campaigns.Add(c);
        }

        private void OpenEdit(Campaign? campaign)
        {
            var vm = new CampaignEditViewModel(campaign, _service);
            vm.OnSaved += (_) => RefreshCampaigns();
            var window = new CampaignEditWindow(vm);
            window.ShowDialog();
        }

        private void Delete(Campaign campaign)
        {
            if (MessageBox.Show($"Delete campaign '{campaign.Name}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _service.Campaigns.Remove(campaign);
                _service.SaveCampaigns();
                RefreshCampaigns();
            }
        }
    }
}
