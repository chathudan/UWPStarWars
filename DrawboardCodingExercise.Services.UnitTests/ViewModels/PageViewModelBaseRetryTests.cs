using System;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.Services;
using DrawboardCodingExercise.Services.UnitTests.TestDoubles;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.Services.UnitTests.ViewModels;

/// <summary>
/// T11, T12: retry re-attempts the whole operation and succeeds once the source recovers;
/// cancel stops after one attempt with no leaked progress event. Exercised via the same probe
/// ViewModel as PageViewModelBaseProgressTests - PageViewModelBase has no concrete subclass yet.
/// </summary>
public class PageViewModelBaseRetryTests
{
	[Fact]
	public async Task RunWithRetryAsync_retries_and_succeeds_on_the_second_attempt()
	{
		var aggregator = new RecordingEventAggregator();
		var userInteraction = Substitute.For<IUserInteractionService>();
		userInteraction.ShowRetryDialogAsync().Returns(RetryDialogResult.Retry);
		var sut = new ProbeViewModel(aggregator, userInteraction);

		var attempt = 0;
		var succeeded = await sut.InvokeRunWithRetryAsync("Loading films", () =>
		{
			attempt++;
			if (attempt == 1)
			{
				throw new InvalidOperationException("first attempt fails");
			}
			return Task.CompletedTask;
		});

		succeeded.ShouldBeTrue();
		attempt.ShouldBe(2);
		await userInteraction.Received(1).ShowRetryDialogAsync();

		// One busy/done pair per attempt: 2 attempts -> 4 events, all matched.
		aggregator.Posted.Count.ShouldBe(4);
	}

	[Fact]
	public async Task RunWithRetryAsync_stops_after_one_attempt_when_the_user_cancels()
	{
		var aggregator = new RecordingEventAggregator();
		var userInteraction = Substitute.For<IUserInteractionService>();
		userInteraction.ShowRetryDialogAsync().Returns(RetryDialogResult.Cancel);
		var sut = new ProbeViewModel(aggregator, userInteraction);

		var attempt = 0;
		var succeeded = await sut.InvokeRunWithRetryAsync("Loading films", () =>
		{
			attempt++;
			throw new InvalidOperationException("always fails");
		});

		succeeded.ShouldBeFalse();
		attempt.ShouldBe(1);
		await userInteraction.Received(1).ShowRetryDialogAsync();

		// Exactly one busy/done pair - cancelling must not leak a progress event.
		aggregator.Posted.Count.ShouldBe(2);
	}

	[Fact]
	public async Task RunWithRetryAsync_does_not_prompt_when_the_work_succeeds_first_try()
	{
		var aggregator = new RecordingEventAggregator();
		var userInteraction = Substitute.For<IUserInteractionService>();
		var sut = new ProbeViewModel(aggregator, userInteraction);

		var succeeded = await sut.InvokeRunWithRetryAsync("Loading films", () => Task.CompletedTask);

		succeeded.ShouldBeTrue();
		await userInteraction.DidNotReceive().ShowRetryDialogAsync();
	}
}
