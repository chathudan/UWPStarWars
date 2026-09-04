using System.Threading.Tasks;

namespace DrawboardCodingExercise.Contracts.Services;

public interface IUserInteractionService
{
	Task<RetryDialogResult> ShowRetryDialogAsync();
}

public enum RetryDialogResult
{
	Retry,
	Cancel
}