using DrawboardCodingExercise.CoreFramework;

namespace DrawboardCodingExercise.View;

/// <summary>
/// The root of the application, handles modal dialogs
/// </summary>
public sealed partial class Shell
{
	public Shell(IFrameNavigator frameNavigator)
	{
		InitializeComponent();
		frameNavigator.Frame = NavFrame;
	}
}