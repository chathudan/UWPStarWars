using DrawboardCodingExercise.Services;

namespace DrawboardCodingExercise.Configuration;

/// <summary>
/// The application configuration, probably stored in LocalAppData normally.
/// </summary>
public class ApplicationConfiguration : IAPISettings
{
	public string ServerAddress { get; } = "https://some.api.com/";
}