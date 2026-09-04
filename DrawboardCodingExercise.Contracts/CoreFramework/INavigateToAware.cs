using System.Threading.Tasks;

namespace DrawboardCodingExercise.Contracts.CoreFramework;

/// <summary>
/// Notifies when the ViewModel has been constructed and attached to a view and is ready to retrieve related data, etc.
/// </summary>
public interface INavigateToAware
{
	/// <summary>
	/// Notify that a navigation has completed
	/// </summary>
	/// <param name="parameter">Any additional data about the navigation</param>
	Task OnNavigatedToAsync(object parameter);
}