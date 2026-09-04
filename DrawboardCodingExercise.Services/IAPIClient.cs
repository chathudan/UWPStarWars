using System.IO;
using System.Threading.Tasks;

namespace DrawboardCodingExercise.Services;

public interface IAPIClient
{
	Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request);
	Task<TResponse> GetAsync<TResponse>(string path);
	Task<Stream> GetImageAsync(string path);
}