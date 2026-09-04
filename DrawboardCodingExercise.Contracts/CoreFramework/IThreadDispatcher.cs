using System;
using System.Threading.Tasks;

namespace DrawboardCodingExercise.Contracts.CoreFramework;

public interface IThreadDispatcher
{
	Task RunOnUIThreadAsync(Func<Task> func);
	bool OnUIThread { get; }
	void FireOnUIAndForget(Action action);
}