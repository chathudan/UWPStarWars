using Newtonsoft.Json;
using Serilog.Context;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.CoreFramework;
using Serilog;

namespace DrawboardCodingExercise.Services;

public class APIClient : IAPIClient
{
	private readonly IAPISettings _apiSettings;
	private readonly JsonSerializerSettings _jsonSerializerSettings;
	private readonly ILogger _logger;
	private static readonly HttpClient Client;

	static APIClient()
	{
		//Initialize the HttpClient statically, as per the Microsoft docs:
		//  HttpClient is intended to be instantiated once and reused throughout the life of an application.
		//  The following conditions can result in SocketException errors:
		//    * Creating a new HttpClient instance per request
		//    * Server under heavy load.
		//
		// see: https://docs.microsoft.com/en-us/aspnet/web-api/overview/advanced/calling-a-web-api-from-a-net-client

		Client = new HttpClient();
		Client.DefaultRequestHeaders.Clear();
		Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		Client.DefaultRequestHeaders.Remove("Authorization");
	}

	public APIClient(
		IAPISettings apiSettings,
		JsonSerializerSettings jsonSerializerSettings, 
		ILogger logger)
	{
		_apiSettings = apiSettings;
		_jsonSerializerSettings = jsonSerializerSettings;
		_logger = logger;
	}
		
	public async Task<TResponse> GetAsync<TResponse>(string path)
	{
		using (LogContext.PushProperty("ResponseType", typeof(TResponse).Name))
		{
			path = path.TrimStart('/');
			var response = await CallService(baseUri =>
				new HttpRequestMessage(HttpMethod.Get, new Uri($"{baseUri}/{path}"))
			);

			var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
			return JsonConvert.DeserializeObject<TResponse>(responseContent, _jsonSerializerSettings);
		}
	}

	public async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request)
	{
		using (LogContext.PushProperty("ResponseType", typeof(TResponse).Name))
		{
			path = path.TrimStart('/');
			var response = await CallService(baseUri =>
			{
				var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri($"{baseUri}/{path}"));
				var content = new StringContent(
					JsonConvert.SerializeObject(request, _jsonSerializerSettings),
					Encoding.UTF8,
					"application/json"
				);
				httpRequestMessage.Content = content;
				return httpRequestMessage;
			});

			var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
			return JsonConvert.DeserializeObject<TResponse>(responseContent, _jsonSerializerSettings);
		}
	}

	public async Task<Stream> GetImageAsync(string path)
	{

		using (LogContext.PushProperty("ResponseType", "Image"))
		{
			path = path.TrimStart('/');
			var response = await CallService(baseUri =>
				new HttpRequestMessage(HttpMethod.Get, new Uri($"{baseUri}/{path}"))
			);

			return await response.ReadAsStreamAsync();
		}

	}

	private async Task<HttpContent> CallService(Func<string, HttpRequestMessage> requestBuilder)
	{
		var request = requestBuilder(_apiSettings.ServerAddress.TrimEnd('/'));

		using (ActionContext.PushActionContext())
		using (LogContext.PushProperty("RequestMethod", request.Method))
		using (LogContext.PushProperty("RequestPath", request.RequestUri))
		{
			var sw = Stopwatch.StartNew();

			request.Headers.Add("X-Correlation-Id", ActionContext.CorrelationId);

			_logger.Verbose("REST {RequestMethod} {RequestPath} called");
			var response = await Client.SendAsync(request, HttpCompletionOption.ResponseContentRead)
				.ConfigureAwait(false);

			sw.Stop();

			using (LogContext.PushProperty("StatusCode", response.StatusCode))
			using (LogContext.PushProperty("Elapsed", sw.Elapsed.TotalMilliseconds))
			{
				switch ((int?) response.StatusCode)
				{
					case var code when code >= 200 && code < 300:
						_logger.Debug("REST {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.000} ms");
						break;
					case var code when code >= 300 && code < 500:
						_logger.Warning("REST {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.000} ms");
						break;
					default:
						_logger.Error("REST {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.000} ms");
						break;
				}
			}

			// Where there is no status specific exception throw a catch-all exception containing the status code.
			if (!response.IsSuccessStatusCode)
			{
				throw new HttpStatusException(response.StatusCode);
			}

			return response.Content;
		}
	}
}