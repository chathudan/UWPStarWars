using System;

namespace DrawboardCodingExercise.Contracts.Services;

public interface IEventAggregator
{
	void Post<T>(T arg) where T : notnull;
	IDisposable Subscribe<T>(Action<T> action);
	IDisposable Subscribe<T>(Action<T> action, Predicate<T> filter);
	IDisposable SubscribeOnUI<T>(Action<T> action);
}