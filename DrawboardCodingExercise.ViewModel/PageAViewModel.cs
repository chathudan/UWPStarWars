using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DrawboardCodingExercise.Contracts.CoreFramework;
using DrawboardCodingExercise.Contracts.Events;
using DrawboardCodingExercise.Contracts.Services;

namespace DrawboardCodingExercise.ViewModel;

public class PageAViewModel : ObservableObject, INavigateToAware, IProvidePageHeader
{
	private readonly IEventAggregator _eventAggregator;

	private readonly string[] _messages =
	{
		"Pretending to do work",
		"Reticulating Splines",
		"Downloading more RAM"
	};

	public PageAViewModel(IEventAggregator eventAggregator)
	{
		_eventAggregator = eventAggregator;
	}

	public async Task OnNavigatedToAsync(object parameter)
	{
		var message = _messages[new Random().Next(_messages.Length)];

		try
		{
			_eventAggregator.Post(new NotifyBusyEvent(message));
			await Task.Delay(5000);
		}
		finally
		{
			_eventAggregator.Post(new NotifyDoneEvent(message));
		}
	}

	public string PageHeader => "PageA";
}