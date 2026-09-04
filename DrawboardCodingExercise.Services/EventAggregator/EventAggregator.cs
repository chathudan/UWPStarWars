using System;
using System.Threading.Tasks.Dataflow;
using DrawboardCodingExercise.Contracts.CoreFramework;
using DrawboardCodingExercise.Contracts.Services;

namespace DrawboardCodingExercise.Services.EventAggregator;

public class EventAggregator : IEventAggregator
{
	private readonly IThreadDispatcher _threadDispatcher;

	public EventAggregator(IThreadDispatcher threadDispatcher)
	{
		_threadDispatcher = threadDispatcher;
	}

	private readonly BroadcastBlock<object> _broadcast = new(arg => arg);
		
	public void Post<T>(T arg) where T : notnull => _broadcast.Post(arg);

	public IDisposable Subscribe<T>(Action<T> action) => _broadcast.LinkTo(
		new ActionBlock<object>(message => action((T)message)),
		arg => arg is T
	);

	public IDisposable SubscribeOnUI<T>(Action<T> action) => _broadcast.LinkTo(
		new ActionBlock<object>(message => _threadDispatcher.FireOnUIAndForget(() => action((T)message))),
		arg => arg is T
	);

	public IDisposable Subscribe<T>(Action<T> action, Predicate<T> filter) => _broadcast.LinkTo(
		new ActionBlock<object>(message => action((T)message)),
		arg => arg is T typedArg && filter(typedArg)
	);
}