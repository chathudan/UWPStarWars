using DrawboardCodingExercise.Contracts.Services;

namespace DrawboardCodingExercise.CoreFramework;

/// <summary>
/// Localizes a key, formatting it as necessary.
/// </summary>
public class LocalizationService : ILocalizationService
{
	public string Translate(string key, params object[] parameters)
	{
		var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
		var localizedString = resourceLoader.GetString(key.Replace('.', '/'));
		return string.IsNullOrEmpty(localizedString)
			? $"[{key}]"
			: string.Format(localizedString, parameters);
	}
}