using System;
using System.Threading;
using Serilog.Context;

namespace DrawboardCodingExercise.Contracts.CoreFramework;

/// <summary>
/// An action context can be used to group together a set of operations under a single correlated ID.
/// This correlation ID should be matchable within remote server logs as well.
/// </summary>
public static class ActionContext
{
	private static readonly AsyncLocal<string> AsyncLocalCorrelationId = new();

	/// <summary>
	/// Request a new action context if one is not in progress
	/// </summary>
	/// <returns>an IDisposable that marks the end of the scope</returns>
	public static IDisposable PushActionContext()
	{
		//If the async local doesn't have a value, we are at the top of a potentially nested chain of calls and should create a new one.
		if (AsyncLocalCorrelationId.Value == null)
		{
			AsyncLocalCorrelationId.Value = Guid.NewGuid().ToString();
			var disposable = LogContext.PushProperty("X-Correlation-Id", AsyncLocalCorrelationId.Value);

			//Disposing of this object should clear the state.
			return new ActionDisposable(() =>
			{
				AsyncLocalCorrelationId.Value = null;
				disposable.Dispose();
			});
		}

		//We are in a nested context, so we don't need to do anything. Disposing of the IDisposable is a No-Op operation.
		return new ActionDisposable(() => { });
	}

	/// <summary>
	/// Returns the current correlation ID
	/// </summary>
	// ReSharper disable once FieldCanBeMadeReadOnly.Global - No Resharper, it really cannot be read only.
	public static string CorrelationId => AsyncLocalCorrelationId.Value ?? string.Empty;

	/// <summary>
	/// Utility class that allows running arbitrary code when disposed.
	/// </summary>
	private class ActionDisposable : IDisposable
	{
		private readonly Action _action;

		public ActionDisposable(Action action)
		{
			_action = action;
		}

		public void Dispose()
		{
			_action();
		}
	}
}