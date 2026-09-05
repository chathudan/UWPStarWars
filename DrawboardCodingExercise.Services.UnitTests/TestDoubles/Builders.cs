using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.Services;
using DrawboardCodingExercise.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DrawboardCodingExercise.Services.UnitTests.TestDoubles;

/// <summary>
/// The exact JsonSerializerSettings the application registers in WebServicesModule.
///
/// Tests deserialize through this rather than through JsonConvert's defaults, because the
/// camel-case contract resolver is precisely what makes snake_case binding non-obvious. A test
/// that used default settings would pass while the real app bound episode_id to 0.
/// </summary>
public static class AppJson
{
	public static JsonSerializerSettings Settings { get; } = new JsonSerializerSettings
	{
		ContractResolver = new CamelCasePropertyNamesContractResolver(),
		Formatting = Formatting.Indented
	};

	public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
}

/// <summary>
/// A hand-written <see cref="IAPIClient"/> double.
///
/// NSubstitute covers most needs, but three behaviours here are awkward to express with it and
/// central to the tests: routing by request path, observing the *peak* number of concurrent
/// in-flight calls, and holding a call open long enough to trip a timeout budget.
/// </summary>
public sealed class FakeApiClient : IAPIClient
{
	private readonly Dictionary<string, string> _responsesByPath = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Exception> _failuresByPath = new(StringComparer.OrdinalIgnoreCase);
	private readonly ConcurrentBag<string> _requestedPaths = new();

	private int _inFlight;
	private int _peakInFlight;

	/// <summary>Delay applied to every call, used to observe concurrency and to trip the budget.</summary>
	public TimeSpan Delay { get; set; } = TimeSpan.Zero;

	/// <summary>Highest number of calls in flight simultaneously across the fake's lifetime.</summary>
	public int PeakConcurrency => Volatile.Read(ref _peakInFlight);

	public IReadOnlyCollection<string> RequestedPaths => _requestedPaths;

	public int CallCount { get; private set; }

	/// <summary>Serves <paramref name="json"/> for the given relative path.</summary>
	public FakeApiClient Returns(string path, string json)
	{
		_responsesByPath[Normalise(path)] = json;
		return this;
	}

	/// <summary>Throws <paramref name="exception"/> for the given relative path.</summary>
	public FakeApiClient Throws(string path, Exception exception)
	{
		_failuresByPath[Normalise(path)] = exception;
		return this;
	}

	/// <summary>Throws for any path not explicitly configured.</summary>
	public Exception DefaultFailure { get; set; }

	public async Task<TResponse> GetAsync<TResponse>(string path)
	{
		var key = Normalise(path);
		_requestedPaths.Add(key);
		CallCount++;

		var current = Interlocked.Increment(ref _inFlight);
		UpdatePeak(current);

		try
		{
			if (Delay > TimeSpan.Zero)
			{
				await Task.Delay(Delay).ConfigureAwait(false);
			}

			if (_failuresByPath.TryGetValue(key, out var failure))
			{
				throw failure;
			}

			if (_responsesByPath.TryGetValue(key, out var json))
			{
				return AppJson.Deserialize<TResponse>(json);
			}

			throw DefaultFailure ?? new InvalidOperationException($"FakeApiClient has no response configured for '{key}'.");
		}
		finally
		{
			Interlocked.Decrement(ref _inFlight);
		}
	}

	public Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request) =>
		throw new NotSupportedException("This feature is read-only; PostAsync is never called.");

	public Task<Stream> GetImageAsync(string path) =>
		throw new NotSupportedException("This feature displays no images; GetImageAsync is never called.");

	private void UpdatePeak(int current)
	{
		int observed;
		while (current > (observed = Volatile.Read(ref _peakInFlight)))
		{
			Interlocked.CompareExchange(ref _peakInFlight, current, observed);
		}
	}

	private static string Normalise(string path) => (path ?? string.Empty).Trim('/');
}

/// <summary>Static API settings pointing at the real base address, so URL normalisation is exercised honestly.</summary>
public sealed class FakeApiSettings : IAPISettings
{
	public FakeApiSettings(string serverAddress = "https://swapi.info/api") => ServerAddress = serverAddress;

	public string ServerAddress { get; }
}

/// <summary>
/// Records every event posted, so progress tests can assert that each busy event is matched by a
/// byte-identical done event. This is asserted rather than assumed because ShellViewModel removes
/// done events by index without a -1 guard: an unmatched pair throws on the UI thread.
/// </summary>
public sealed class RecordingEventAggregator : IEventAggregator
{
	private readonly List<object> _posted = new();
	private readonly object _gate = new();

	public IReadOnlyList<object> Posted
	{
		get { lock (_gate) { return _posted.ToArray(); } }
	}

	public void Post<T>(T arg) where T : notnull
	{
		lock (_gate) { _posted.Add(arg); }
	}

	public IDisposable Subscribe<T>(Action<T> action) => NullSubscription.Instance;
	public IDisposable Subscribe<T>(Action<T> action, Predicate<T> filter) => NullSubscription.Instance;
	public IDisposable SubscribeOnUI<T>(Action<T> action) => NullSubscription.Instance;

	private sealed class NullSubscription : IDisposable
	{
		public static readonly NullSubscription Instance = new();
		public void Dispose() { }
	}
}

/// <summary>Returns the key itself, so tests assert on stable keys instead of translated prose.</summary>
public sealed class EchoLocalizationService : ILocalizationService
{
	public string Translate(string key, params object[] parameters) =>
		parameters is { Length: > 0 } ? $"{key}:{string.Join(",", parameters)}" : key;
}
