using System.Diagnostics;
using Xunit;

namespace DrawboardCodingExercise.Services.UnitTests;

public class XUnitTests
{
	[Fact]
	public void EnsureTestsRun()
	{
		// A nonsensical test to ensure that when we update NuGet packages or UWP SDK versions, that the test frameworks still run.
		Debug.Assert(true);
	}
}