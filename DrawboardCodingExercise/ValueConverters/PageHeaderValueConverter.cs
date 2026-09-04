using System;
using Windows.UI.Xaml.Data;
using DrawboardCodingExercise.Contracts.CoreFramework;

namespace DrawboardCodingExercise.ValueConverters;

/// <summary>
/// For Pages with a ViewModel that implements <see cref="IProvidePageHeader"/>, the header key provided by the ViewModel is localized
/// </summary>
public class PageHeaderValueConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is IProvidePageHeader page)
		{
			var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView("Resources");
			var localizedString = resourceLoader.GetString($"PageHeader/{page.PageHeader}/Text");
			if (string.IsNullOrEmpty(localizedString))
			{
				return $"[PageHeader.{page.PageHeader}.Text]";
			}

			return localizedString;
		}

		return string.Empty;
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language)
	{
		throw new NotSupportedException("This is a one-way converter");
	}
}