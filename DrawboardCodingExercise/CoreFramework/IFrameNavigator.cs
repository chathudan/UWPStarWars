using Windows.UI.Xaml.Controls;

namespace DrawboardCodingExercise.CoreFramework;

/// <summary>
/// The FrameNavigator is responsible for delegating the control Frame present in a Shell, or perhaps a nested view.
/// </summary>
public interface IFrameNavigator
{
	Frame Frame { set; }
}