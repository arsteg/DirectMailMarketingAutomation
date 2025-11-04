using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace MailMergeUI.Helpers
{
    public class DateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss");
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class TimeSpanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan ts)
                return DateTime.Today.Add(ts).ToString("hh:mm tt");
            return null!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && DateTime.TryParse(s, out var dt))
                return dt.TimeOfDay;
            return TimeSpan.Zero;
        }
    }

    public class DayOfWeekInListConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is List<DayOfWeek> list && parameter is DayOfWeek day)
                return list.Contains(day);
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && parameter is DayOfWeek day)
                return new Tuple<DayOfWeek, bool>(day, isChecked);
            return null;
        }
    }

    //public class TimeSpanToStringConverter : IValueConverter
    //{
    //    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    //    {
    //        if (value is TimeSpan time)
    //            return DateTime.Today.Add(time).ToString("hh:mm tt", culture);

    //        return string.Empty;
    //    }

    //    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    //    {
    //        if (value is string str && !string.IsNullOrWhiteSpace(str))
    //        {
    //            if (DateTime.TryParseExact(str,
    //                                       new[] { "h:mm tt", "hh:mm tt" },
    //                                       culture,
    //                                       DateTimeStyles.None,
    //                                       out var dt))
    //                return dt.TimeOfDay;

    //            if (TimeSpan.TryParse(str, out var ts))
    //                return ts;
    //        }

    //        return Binding.DoNothing;
    //    }
    //}

}
