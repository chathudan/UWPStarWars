using System;
using System.Threading.Tasks;
using Windows.UI.Popups;
using DrawboardCodingExercise.Contracts.Services;

namespace DrawboardCodingExercise.CoreFramework;

public class UserInteractionService : IUserInteractionService
{
	private readonly ILocalizationService _localizationService;

	public UserInteractionService(ILocalizationService localizationService)
	{
		_localizationService = localizationService;
	}

	public async Task<RetryDialogResult> ShowRetryDialogAsync()
	{
		var dialog = new MessageDialog(_localizationService.Translate("Errors.Retry"));

		var dialogResult = RetryDialogResult.Retry;

		IUICommand retryCommand = new UICommand(_localizationService.Translate("Dialog.RetryButton.Text"), command => dialogResult = RetryDialogResult.Retry);
		IUICommand cancelCommand = new UICommand(_localizationService.Translate("Dialog.CancelButton.Text"), command => dialogResult = RetryDialogResult.Cancel);

		dialog.Commands.Add(retryCommand);
		dialog.Commands.Add(cancelCommand);

		await dialog.ShowAsync();

		return dialogResult;
	}
}