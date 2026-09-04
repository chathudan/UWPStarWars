using System;
using System.Net;

namespace DrawboardCodingExercise.Services;

/// <summary>
/// The exception that is thrown when an HTTP request returns a status code not in the 2xx range.
/// </summary>
public class HttpStatusException : Exception
{
	public HttpStatusException(HttpStatusCode statusCode)
	{
		StatusCode = statusCode;
	}

	public HttpStatusCode StatusCode { get; }
}