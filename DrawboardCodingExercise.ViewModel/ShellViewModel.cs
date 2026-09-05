using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawboardCodingExercise.Contracts;
using DrawboardCodingExercise.Contracts.CoreFramework;
using DrawboardCodingExercise.Contracts.Events;
using DrawboardCodingExercise.Contracts.Services;

namespace DrawboardCodingExercise.ViewModel;

public partial class ShellViewModel : ObservableObject, INavigateToAware, IDisposable
{
	private readonly INavigationService _navigationService;
	private readonly IEventAggregator _eventAggregator;
		
	private readonly CompositeDisposable _subscriptions = new();
	private readonly ObservableCollection<string> _thingsInProgress = new();
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _thingInProgress;

	public ShellViewModel(
		INavigationService navigationService,
		IEventAggregator eventAggregator)
	{
		_navigationService = navigationService;
		_eventAggregator = eventAggregator;
		_navigationService.Navigated += NavigationService_OnNavigated;
	}

	public bool CanGoBack => _navigationService.CanGoBack;

	[RelayCommand(CanExecute = nameof(CanGoBack))]
	private async Task OnGoBack()
	{
		await _navigationService.BackAsync();
	}

	private void NavigationService_OnNavigated()
	{
		GoBackCommand.NotifyCanExecuteChanged();
		OnPropertyChanged(nameof(CanGoBack));
	}

	public async Task OnNavigatedToAsync(object parameter)
	{
		_subscriptions.Add(_eventAggregator.SubscribeOnUI<NotifyBusyEvent>(OnNotifyBusy));
		_subscriptions.Add(_eventAggregator.SubscribeOnUI<NotifyDoneEvent>(OnNotifyDone));
			
		await _navigationService.NavigateAsync(PageKey.Films).ConfigureAwait(true);
	}

	private void OnNotifyDone(NotifyDoneEvent obj)
	{
		var index = _thingsInProgress.IndexOf(obj.Event);
		_thingsInProgress.RemoveAt(index);

		IsBusy = _thingsInProgress.Count > 0;
		ThingInProgress = _thingsInProgress.FirstOrDefault();
	}

	private void OnNotifyBusy(NotifyBusyEvent obj)
	{
		_thingsInProgress.Add(obj.Event);
		IsBusy = true;
		ThingInProgress = _thingsInProgress.FirstOrDefault();
	}

	public void Dispose()
	{
		_subscriptions.Dispose();
	}
}