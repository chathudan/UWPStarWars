using DrawboardCodingExercise.Services.StarWars;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.Services.UnitTests.StarWars;

/// <summary>
/// T30, T31: URL normalisation must keep IAPIClient as the only HTTP surface (AC-006) and never
/// throw on an unexpected shape.
/// </summary>
public class SwapiResourcePathTests
{
	private const string Base = "https://swapi.info/api";

	[Theory]
	[InlineData("https://swapi.info/api/people/1", Base, "people/1")]
	[InlineData("https://swapi.info/api/films/4", Base, "films/4")]
	[InlineData("https://SWAPI.INFO/API/people/1", Base, "people/1")] // case-insensitive base match
	[InlineData("https://swapi.info/api/people/1", "https://swapi.info/api/", "people/1")] // trailing slash on base
	[InlineData("https://swapi.info/api/people/1/", Base, "people/1")] // trailing slash on url
	public void ToRelativePath_normalises_absolute_urls_under_the_configured_base(string url, string serverAddress, string expected)
	{
		var actual = SwapiResourcePath.ToRelativePath(url, serverAddress);

		actual.ShouldBe(expected);
	}

	// T31: a URL outside the configured base must be rejected, not passed through raw.
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("https://not-swapi.example/api/people/1")]
	[InlineData("not a url at all")]
	public void ToRelativePath_returns_null_for_null_blank_or_out_of_base_urls(string url)
	{
		var actual = SwapiResourcePath.ToRelativePath(url, Base);

		actual.ShouldBeNull();
	}

	[Theory]
	[InlineData("https://swapi.info/api/films/1", "1")]
	[InlineData("https://swapi.info/api/people/42/", "42")]
	public void ExtractId_returns_the_trailing_identifier_segment(string url, string expected)
	{
		var actual = SwapiResourcePath.ExtractId(url);

		actual.ShouldBe(expected);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("not a url at all")]
	public void ExtractId_returns_null_for_unparseable_input(string url)
	{
		var actual = SwapiResourcePath.ExtractId(url);

		actual.ShouldBeNull();
	}
}
