using DrawboardCodingExercise.Services;

namespace DrawboardCodingExercise.Configuration;

/// <summary>
/// The application configuration, probably stored in LocalAppData normally.
/// </summary>
public class ApplicationConfiguration : IAPISettings
{
	// The Star Wars API. Note that this mirror returns a bare JSON array from /films (no paged
	// envelope), and resource links inside a film are absolute URLs under this same base.
	public string ServerAddress { get; } = "https://swapi.info/api";
}