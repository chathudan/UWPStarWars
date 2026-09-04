using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using DrawboardCodingExercise.Contracts.CoreFramework;
using Serilog;

namespace DrawboardCodingExercise.CoreFramework;

/// <summary>
///     Delegates the running of a set of code to happening on the UI thread.
/// </summary>
public class ThreadDispatcher : IThreadDispatcher
{
	private readonly ILogger _logger;

	public ThreadDispatcher(ILogger logger)
	{
		_logger = logger;
	}

	/// <summary>
	///     Forces a block of code to run on the UI thread. If the code is already running on the UI thread, it is invoked
	///     immediately.
	/// </summary>
	/// <param name="function">The code to run</param>
	public Task RunOnUIThreadAsync(Func<Task> function)
	{
		var coreApplicationView = CoreApplication.MainView;
		var dispatcher = coreApplicationView.Dispatcher;

		if (dispatcher.HasThreadAccess)
		{
			return function();
		}

		return dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => function()).AsTask();
	}

	/// <summary>
	///     Forces a block of code to run on the UI thread. If the code is already running on the UI thread, it is invoked
	///     immediately.
	/// </summary>
	/// <param name="function">The code to run</param>
	public void FireOnUIAndForget(Action function)
	{
		var coreApplicationView = CoreApplication.MainView;
		var dispatcher = coreApplicationView.Dispatcher;

		if (dispatcher.HasThreadAccess)
		{
			function();
		}
		else
		{
			_ = Task.Run(async () =>
			{
				try
				{
					await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => function());
				}
				catch (Exception e)
				{
					_logger.Error(e, "Error in a FireOnUIAndForget operation");
				}
			});
		}
	}

	public bool OnUIThread => CoreApplication.MainView?.Dispatcher?.HasThreadAccess ??
	                          throw new Exception(
		                          "Attempt to dispatch to the UI thread before the dispatcher was available");
}