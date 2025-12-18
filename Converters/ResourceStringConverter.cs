using System;
using System.Globalization;
using System.Windows.Data;
using BluetoothManager.Helpers;

namespace BluetoothManager.Converters
{
    public class ResourceStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string key)
                return StringResources.GetString(key) ?? key;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
