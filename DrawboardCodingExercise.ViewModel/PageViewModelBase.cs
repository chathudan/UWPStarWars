using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DrawboardCodingExercise.Contracts.Events;
using DrawboardCodingExercise.Contracts.Services;

namespace DrawboardCodingExercise.ViewModel;

/// <summary>
/// Owns the busy/done event pairing and (from Cycle 8) the retry/cancel loop shared by every
/// page ViewModel that performs a retrieval.
///
/// This is a defensive class, not a stylistic one: ShellViewModel.OnNotifyDone does
/// `_thingsInProgress.RemoveAt(IndexOf(...))` with no -1 guard, so a NotifyDoneEvent whose
/// string does not exactly match a live NotifyBusyEvent throws on the UI thread. Capturing the
/// message into a single local and posting both events from it makes that mismatch structurally
/// impossible rather than a convention someone must remember (research.md R4).
/// </summary>
public abstract class PageViewModelBase : ObservableObject
{
	private readonly IEventAggregator _eventAggregator;
	private readonly IUserInteractionService _userInteractionService;

	protected PageViewModelBase(IEventAggregator eventAggregator, IUserInteractionService userInteractionService)
	{
		_eventAggregator = eventAggregator;
		_userInteractionService = userInteractionService;
	}

	/// <summary>
	/// Runs <paramref name="work"/> bracketed by a matched NotifyBusyEvent/NotifyDoneEvent pair.
	/// The done event is posted in <c>finally</c>, so progress clears whether <paramref name="work"/>
	/// completes, throws, or the caller cancels around it (FR-019, AC-009).
	/// </summary>
	protected async Task RunBusyAsync(string message, Func<Task> work)
	{
		// Captured into a local once, then posted from that same local on both sides - this is
		// what makes the busy/done pairing byte-identical by construction rather than by
		// discipline (AC-009).
		_eventAggregator.Post(new NotifyBusyEvent(message));
		try
		{
			await work().ConfigureAwait(true);
		}
		finally
		{
			_eventAggregator.Post(new NotifyDoneEvent(message));
		}
	}

	/// <summary>
	/// Runs <paramref name="work"/> via <see cref="RunBusyAsync"/>; on failure, offers
	/// retry/cancel through <see cref="IUserInteractionService"/>. Retry re-attempts the whole
	/// operation (a fresh busy/done pair per attempt); Cancel stops the loop and returns
	/// <c>false</c> so the caller can render its error state. Returns <c>true</c> on success.
	/// </summary>
	protected async Task<bool> RunWithRetryAsync(string busyMessage, Func<Task> work)
	{
		while (true)
		{
			try
			{
				await RunBusyAsync(busyMessage, work).ConfigureAwait(true);
				return true;
			}
			catch (Exception)
			{
				var choice = await _userInteractionService.ShowRetryDialogAsync().ConfigureAwait(true);
				if (choice == RetryDialogResult.Cancel)
				{
					return false;
				}

				// Retry: loop back for a fresh attempt, with its own busy/done pair.
			}
		}
	}
}
