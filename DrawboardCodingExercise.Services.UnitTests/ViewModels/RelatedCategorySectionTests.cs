using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DrawboardCodingExercise.Services.UnitTests.TestDoubles;
using DrawboardCodingExercise.ViewModel;
using DrawboardCodingExercise.ViewModel.Models;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.Services.UnitTests.ViewModels;

/// <summary>
/// T39, T40 (FR-028, V13): when a related-category section is allowed to issue a request.
///
/// These two tests earn their place because their failure modes are INVISIBLE on screen. A
/// section that loads eagerly, or re-requests on every toggle, shows the user exactly the same
/// names as one that behaves correctly - only the request count differs. Manual validation
/// cannot see the difference; a counted fake can.
///
/// The section is exercised directly with a counting loader rather than through the ViewModel,
/// because what is under test is the section's decision about *when* to load, not the
/// ViewModel's machinery for *how*.
/// </summary>
public class RelatedCategorySectionTests
{
	private static RelatedCategorySection CreateSut(
		IReadOnlyList<string> urls,
		Func<RelatedCategorySection, Task> load)
	{
		return new RelatedCategorySection(RelatedCategory.Planets, urls, new EchoLocalizationService(), load);
	}

	/// <summary>A loader that behaves as the real one does on success, and counts its calls.</summary>
	private static Func<RelatedCategorySection, Task> SucceedingLoader(Action onCall = null) => section =>
	{
		onCall?.Invoke();
		section.ApplyResult(new[] { new RelatedResourceListItem("Tatooine", "planets/1") }, isPartial: false);
		return Task.CompletedTask;
	};

	/// <summary>A loader that fails the way the ViewModel's does after the user cancels retry.</summary>
	private static Func<RelatedCategorySection, Task> FailingLoader(Action onCall = null) => section =>
	{
		onCall?.Invoke();
		section.State = PageLoadState.Error;
		return Task.CompletedTask;
	};

	// T39
	[Fact]
	public async Task Expanding_a_collapsed_section_loads_it_exactly_once()
	{
		var calls = 0;
		var sut = CreateSut(new[] { "planets/1" }, SucceedingLoader(() => calls++));

		sut.State.ShouldBe(PageLoadState.NotStarted);

		await sut.ToggleCommand.ExecuteAsync(null);

		sut.IsExpanded.ShouldBeTrue();
		sut.State.ShouldBe(PageLoadState.Loaded);
		calls.ShouldBe(1);
	}

	[Fact]
	public async Task Collapsing_and_re_expanding_a_loaded_section_does_not_request_again()
	{
		var calls = 0;
		var sut = CreateSut(new[] { "planets/1" }, SucceedingLoader(() => calls++));

		await sut.ToggleCommand.ExecuteAsync(null);   // expand + load
		await sut.ToggleCommand.ExecuteAsync(null);   // collapse
		await sut.ToggleCommand.ExecuteAsync(null);   // re-expand

		sut.IsExpanded.ShouldBeTrue();
		sut.State.ShouldBe(PageLoadState.Loaded);
		sut.Items.Count.ShouldBe(1);
		calls.ShouldBe(1, "a section that has already loaded must show what it has, not re-request it");
	}

	[Fact]
	public async Task Expanding_a_section_the_film_references_nothing_for_goes_straight_to_empty_without_requesting()
	{
		var calls = 0;
		var sut = CreateSut(Array.Empty<string>(), SucceedingLoader(() => calls++));

		await sut.ToggleCommand.ExecuteAsync(null);

		sut.State.ShouldBe(PageLoadState.Empty);
		calls.ShouldBe(0, "there is nothing to fetch, so no request should be issued at all");
	}

	// T40: a failed section must not be silently retried by re-expansion. A user idly toggling a
	// broken section would otherwise hammer a failing endpoint and be shown the retry/cancel
	// prompt again on a gesture they did not intend as a retry.
	[Fact]
	public async Task Re_expanding_a_failed_section_does_not_silently_retry_it()
	{
		var calls = 0;
		var sut = CreateSut(new[] { "planets/1" }, FailingLoader(() => calls++));

		await sut.ToggleCommand.ExecuteAsync(null);   // expand + fail
		sut.State.ShouldBe(PageLoadState.Error);

		await sut.ToggleCommand.ExecuteAsync(null);   // collapse
		await sut.ToggleCommand.ExecuteAsync(null);   // re-expand

		calls.ShouldBe(1, "only the section's own Retry may re-attempt a failed load");
		sut.State.ShouldBe(PageLoadState.Error);
	}

	[Fact]
	public async Task The_section_retry_command_re_attempts_a_failed_load()
	{
		var calls = 0;
		var succeedAfterFirst = new Func<RelatedCategorySection, Task>(section =>
		{
			calls++;
			if (calls == 1)
			{
				section.State = PageLoadState.Error;
			}
			else
			{
				section.ApplyResult(new[] { new RelatedResourceListItem("Tatooine", "planets/1") }, isPartial: false);
			}

			return Task.CompletedTask;
		});

		var sut = CreateSut(new[] { "planets/1" }, succeedAfterFirst);

		await sut.ToggleCommand.ExecuteAsync(null);
		sut.State.ShouldBe(PageLoadState.Error);

		await sut.RetryCommand.ExecuteAsync(null);

		calls.ShouldBe(2);
		sut.State.ShouldBe(PageLoadState.Loaded);
		sut.Items.Count.ShouldBe(1);
	}
}
