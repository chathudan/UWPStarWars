using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace DrawboardCodingExercise.ValueConverters;

public class BoolToVisibilityConverter : IValueConverter
{
	public Visibility IfTrueThen { get; set; } = Visibility.Visible;
	public Visibility IfFalseThen { get; set; } = Visibility.Collapsed;

	public object Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is null)
		{
			return IfFalseThen;
		}

		if (value is bool theBoolean)
		{
			return theBoolean ? IfTrueThen : IfFalseThen;
		}

		throw new NotSupportedException($"Attempted to convert an unsupported object of type {value.GetType()}");
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language)
	{
		return Equals(value, IfTrueThen);
	}
}