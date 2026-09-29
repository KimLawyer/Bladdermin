using BladderMin;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace VMS.TPS.Converters
{
    public class VisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool? V = value as bool?;
            if (V == null)
                return Visibility.Collapsed;
            if (V == true)
                return Visibility.Visible;
            else
                return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            Visibility? V = value as Visibility?;
            if (V == Visibility.Hidden)
                return false;
            else
                return true;
        }
    }
    public class BoolToString : IValueConverter
    {
        public object Convert(object value, Type targetType, object paramter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return false;
            else
            {
                return value.ToString();
            }
        }

        public object ConvertBack(object value, Type targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            bool boolValue = (bool)value;
            return boolValue;
        }

    }
}
