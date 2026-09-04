using System;
using System.Threading.Tasks;

namespace DrawboardCodingExercise.Contracts.CoreFramework;

/// <summary>
/// The navigation service enhances the navigation capability of a Frame in UWP.
///
/// It has the additional responsibility of resolving the View and ViewModel from a PageKey and ensuring
/// that navigation happens on the UI Thread.
///
/// If a ViewModel implements <see cref="INavigateToAware"/>, the <see cref="INavigateToAware.OnNavigatedToAsync"/>
/// method is invoked after the navigation has occurred. 
/// </summary>
public interface INavigationService
{
	/// <summary>
	/// Causes a navigation to a page
	/// </summary>
	/// <param name="pageKey">The key that identifies the page</param>
	/// <param name="parameter">any object that represents the parameters being passed.</param>
	Task NavigateAsync(PageKey pageKey, object parameter = null);

	/// <summary>
	/// Requests the navigation service to go back one page
	/// </summary>
	Task BackAsync();

	event Action Navigated;
	bool CanGoBack { get; }
}