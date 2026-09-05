using System;

namespace DrawboardCodingExercise.Services.StarWars;

/// <summary>
/// Normalises the absolute SWAPI URLs found inside film records (e.g. in the "characters" array)
/// against the application's configured base address, so that IAPIClient - which composes
/// requests from a base address plus a relative path - remains the only HTTP surface in the app.
/// </summary>
internal static class SwapiResourcePath
{
	/// <summary>
	/// Converts an absolute SWAPI URL into a path relative to <paramref name="serverAddress"/>.
	/// Returns null - never throws - when the URL is null, blank, or not under the given base.
	/// </summary>
	public static string ToRelativePath(string absoluteUrl, string serverAddress)
	{
		if (string.IsNullOrWhiteSpace(absoluteUrl) || string.IsNullOrWhiteSpace(serverAddress))
		{
			return null;
		}

		var trimmedBase = serverAddress.TrimEnd('/');
		var trimmedUrl = absoluteUrl.Trim().TrimEnd('/');

		if (trimmedUrl.Length <= trimmedBase.Length ||
		    !trimmedUrl.StartsWith(trimmedBase, StringComparison.OrdinalIgnoreCase) ||
		    trimmedUrl[trimmedBase.Length] != '/')
		{
			return null;
		}

		return trimmedUrl.Substring(trimmedBase.Length + 1);
	}

	/// <summary>
	/// Extracts the trailing identifier segment from an absolute SWAPI URL
	/// (e.g. "https://swapi.info/api/films/1" -&gt; "1"). Returns null when unparseable.
	/// </summary>
	public static string ExtractId(string absoluteUrl)
	{
		if (string.IsNullOrWhiteSpace(absoluteUrl))
		{
			return null;
		}

		var trimmed = absoluteUrl.Trim().TrimEnd('/');
		var lastSlash = trimmed.LastIndexOf('/');

		if (lastSlash < 0 || lastSlash == trimmed.Length - 1)
		{
			return null;
		}

		var segment = trimmed.Substring(lastSlash + 1);
		return segment.Length == 0 ? null : segment;
	}
}
