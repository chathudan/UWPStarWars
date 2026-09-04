namespace DrawboardCodingExercise.Contracts.Services;

/// <summary>
/// Localizes text
/// </summary>
public interface ILocalizationService
{
	/// <summary>
	/// Localizes a string by it's key, formatting with the provided parameters.
	/// </summary>
	/// <param name="key">The key into the strings resw file</param>
	/// <param name="parameters">Any parameters to the text</param>
	/// <returns>The translated text, or [key] if it failed</returns>
	string Translate(string key, params object[] parameters);
}