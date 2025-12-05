using KakaoBotAT.MobileClient.ViewModels;
using System.Globalization;

namespace KakaoBotAT.MobileClient;

public class BooleanToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRunning && parameter is string text)
        {
            var parts = text.Split('|');
            if (parts.Length == 2)
            {
                return isRunning ? parts[0] : parts[1];
            }
        }
        return "N/A";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BooleanToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRunning && parameter is string colors)
        {
            var parts = colors.Split('|');
            if (parts.Length == 2)
            {
                var trueColor = parts[0];
                var falseColor = parts[1];
                return isRunning ? Color.Parse(trueColor) : Color.Parse(falseColor);
            }
        }
        return Color.FromArgb("#000000"); 
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        Resources.Add("BooleanToTextConverter", new BooleanToTextConverter());
        Resources.Add("BooleanToColorConverter", new BooleanToColorConverter());
        Resources.Add("InverseBooleanConverter", new InverseBooleanConverter());

        InitializeComponent();

        BindingContext = viewModel;
    }
}