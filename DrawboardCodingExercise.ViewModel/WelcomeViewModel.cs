using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawboardCodingExercise.Contracts;
using DrawboardCodingExercise.Contracts.CoreFramework;

namespace DrawboardCodingExercise.ViewModel;

public partial class WelcomeViewModel : ObservableObject, IProvidePageHeader
{
	private readonly INavigationService _navigationService;

	public WelcomeViewModel(INavigationService navigationService)
	{
		_navigationService = navigationService;
	}

	[RelayCommand]
	private async Task OnNavigateToChild()
	{
		await _navigationService.NavigateAsync(PageKey.PageA);
	}

	public string PageHeader => "Welcome";
}