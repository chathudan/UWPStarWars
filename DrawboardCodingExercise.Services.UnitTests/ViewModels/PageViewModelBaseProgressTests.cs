using System;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.Events;
using DrawboardCodingExercise.Services.UnitTests.TestDoubles;
using DrawboardCodingExercise.ViewModel;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.Services.UnitTests.ViewModels;

/// <summary>
/// A minimal subclass that exposes the protected RunBusyAsync for testing. It carries no
/// behaviour of its own - PageViewModelBase is the thing under test.
/// </summary>
internal sealed class ProbeViewModel : PageViewModelBase
{
	public ProbeViewModel(DrawboardCodingExercise.Contracts.Services.IEventAggregator eventAggregator) : base(eventAggregator) { }

	public Task RunAsync(string message, Func<Task> work) => RunBusyAsync(message, work);
}

/// <summary>
/// T13-T16: every busy event is matched by a byte-identical done event, posted in `finally` so
/// progress clears on success, on a handled failure, and on a wholly unexpected exception alike
/// (FR-019, AC-009). This is asserted at the base-class level via a probe ViewModel; T13/T16 are
/// asserted again against the real ViewModels once they exist, since a ViewModel that forgets to
/// call RunBusyAsync would otherwise pass the whole suite while leaving the shell's progress ring
/// spinning forever.
/// </summary>
public class PageViewModelBaseProgressTests
{
	[Fact]
	public async Task RunBusyAsync_posts_one_matched_busy_and_done_pair_on_success()
	{
		var aggregator = new RecordingEventAggregator();
		var sut = new ProbeViewModel(aggregator);

		await sut.RunAsync("Loading films", () => Task.CompletedTask);

		aggregator.Posted.Count.ShouldBe(2);
		aggregator.Posted[0].ShouldBeOfType<NotifyBusyEvent>().Event.ShouldBe("Loading films");
		aggregator.Posted[1].ShouldBeOfType<NotifyDoneEvent>().Event.ShouldBe("Loading films");
	}

	[Fact]
	public async Task RunBusyAsync_still_posts_the_done_event_when_the_work_throws_a_handled_failure()
	{
		var aggregator = new RecordingEventAggregator();
		var sut = new ProbeViewModel(aggregator);

		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.RunAsync("Loading films", () => throw new InvalidOperationException("recoverable")));

		aggregator.Posted.Count.ShouldBe(2);
		((NotifyDoneEvent)aggregator.Posted[1]).Event.ShouldBe("Loading films");
	}

	[Fact]
	public async Task RunBusyAsync_still_posts_the_done_event_on_a_wholly_unexpected_exception()
	{
		var aggregator = new RecordingEventAggregator();
		var sut = new ProbeViewModel(aggregator);

		await Should.ThrowAsync<NullReferenceException>(
			() => sut.RunAsync("Loading films", () => throw new NullReferenceException("unexpected bug")));

		aggregator.Posted.Count.ShouldBe(2);
		((NotifyDoneEvent)aggregator.Posted[1]).Event.ShouldBe("Loading films");
	}

	[Fact]
	public async Task RunBusyAsync_posts_byte_identical_busy_and_done_strings()
	{
		var aggregator = new RecordingEventAggregator();
		var sut = new ProbeViewModel(aggregator);
		const string message = "Loading The Empire Strikes Back";

		await sut.RunAsync(message, () => Task.CompletedTask);

		var busy = (NotifyBusyEvent)aggregator.Posted[0];
		var done = (NotifyDoneEvent)aggregator.Posted[1];
		busy.Event.ShouldBe(done.Event);
	}
}
