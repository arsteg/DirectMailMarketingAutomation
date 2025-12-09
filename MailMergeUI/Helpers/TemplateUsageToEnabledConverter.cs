// TemplateUsageToEnabledConverter.cs
using MailMerge.Data.Models;
using MailMergeUI.ViewModels;
using System;
using System.Globalization;
using System.Windows.Data;

public class TemplateUsageToEnabledConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TemplateListViewModel vm || parameter is not Template template)
            return false;

        return vm.CanDeleteTemplate(template);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}