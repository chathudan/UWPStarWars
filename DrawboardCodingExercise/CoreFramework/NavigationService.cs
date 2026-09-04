using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Autofac;
using DrawboardCodingExercise.Contracts;
using DrawboardCodingExercise.Contracts.CoreFramework;
using Serilog;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DrawboardCodingExercise.CoreFramework;

/// <summary>
/// The navigation service enhances the navigation capability of a Frame in UWP.
///
/// It has the additional responsibility of resolving the View and ViewModel from a PageKey and ensuring
/// that navigation happens on the UI Thread.
///
/// If a ViewModel implements <see cref="INavigateToAware"/>, the <see cref="INavigateToAware.OnNavigatedToAsync"/>
/// method is invoked after the navigation has occurred. 
/// </summary>
public class NavigationService : IFrameNavigator, INavigationService
{
	private readonly IComponentContext _componentContext;
	private readonly ILogger _logger;
	private readonly IThreadDispatcher _threadDispatcher;
	private Frame _frame;

	public event Action Navigated;
	public NavigationService(IComponentContext componentContext, ILogger logger, IThreadDispatcher threadDispatcher)
	{
		_componentContext = componentContext;
		_logger = logger;
		_threadDispatcher = threadDispatcher;
	}

	public bool CanGoBack => _frame.BackStackDepth > 0;

	/// <summary>
	/// The frame that will be managed by this NavigationService
	/// </summary>
	public Frame Frame
	{
		set => _frame = value;
	}

	/// <summary>
	/// Causes a navigation to a page
	/// </summary>
	/// <param name="pageKey">The key that identifies the page</param>
	/// <param name="parameter">any object that represents the parameters being passed.</param>
	public async Task NavigateAsync(PageKey pageKey, object parameter = null)
	{
		var logger = _logger.ForContext("Parameter", parameter, true);
		if (!_threadDispatcher.OnUIThread)
		{
			await _threadDispatcher.RunOnUIThreadAsync(() => NavigateAsync(pageKey, parameter))
				.ConfigureAwait(true);
			return;
		}

		var pageTimer = Stopwatch.StartNew();
		try
		{
			var pageType = _componentContext.ResolveKeyed<Type>(pageKey);

			var targetPageNavigationDetails = new NavigationDetails(pageKey, parameter);

			var viewModel = _componentContext.ResolveOptionalKeyed<ObservableObject>(pageKey);

			var navigationSuccessful = _frame.Navigate(pageType, targetPageNavigationDetails);
			Navigated?.Invoke();
			if (navigationSuccessful && _frame.Content is FrameworkElement newContent)
			{
				_componentContext.InjectUnsetProperties(newContent);
				newContent.DataContext = viewModel;
			}

			if (navigationSuccessful && viewModel is INavigateToAware navigateToAware)
			{
				var navigateToTimer = Stopwatch.StartNew();
				await navigateToAware.OnNavigatedToAsync(parameter).ConfigureAwait(true);
				logger = logger.ForContext("OnNavigatedToDuration", navigateToTimer.ElapsedMilliseconds);
			}

			logger = logger.ForContext("TotalPageLoadDuration", pageTimer.ElapsedMilliseconds);

			logger.Information("Navigated to {PageKey} Page", pageKey);
		}
		catch (Exception e)
		{
			logger.Fatal(e, "A page of type {PageKey} did not properly handle navigation", pageKey);

			throw;
		}
	}

	public async Task BackAsync()
	{
		var lastEntry = _frame.BackStack.LastOrDefault();
		var logger = _logger;
		if (lastEntry is null) return;
		var navigationDetails = (NavigationDetails)lastEntry.Parameter;
		var pageKey = navigationDetails.PageKey;
		try
		{
			var pageTimer = Stopwatch.StartNew();
			var viewModel = _componentContext.ResolveOptionalKeyed<ObservableObject>(pageKey);
			logger = logger.ForContext("Parameter", navigationDetails.Parameter, true);

			_frame.GoBack();
			if (_frame.Content is FrameworkElement newContent)
			{
				_componentContext.InjectUnsetProperties(newContent);
				newContent.DataContext = viewModel;
			}

			if (viewModel is INavigateToAware navigateToAware)
			{
				var navigateToTimer = Stopwatch.StartNew();
				await navigateToAware.OnNavigatedToAsync(navigationDetails.Parameter).ConfigureAwait(true);
				logger = logger.ForContext("OnNavigatedToDuration", navigateToTimer.ElapsedMilliseconds);
			}

			logger = logger.ForContext("TotalPageLoadDuration", pageTimer.ElapsedMilliseconds);

			logger.Information("Navigated Back to {PageKey}", pageKey);
		}
		catch (Exception e)
		{
			logger.Fatal(e, "A page of type {PageKey} did not properly handle navigation back", pageKey);

			throw;
		}
	}
}