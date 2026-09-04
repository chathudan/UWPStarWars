using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Autofac;
using AutofacSerilogIntegration;
using DrawboardCodingExercise.Configuration;
using DrawboardCodingExercise.View;
using DrawboardCodingExercise.ViewModel;
using Serilog;

namespace DrawboardCodingExercise;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
sealed partial class App
{
	private IContainer _container;

	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		InitializeComponent();
		Suspending += OnSuspending;
	}

	/// <summary>
	/// Invoked when the application is launched normally by the end user.  Other entry points
	/// will be used such as when the application is launched to open a specific file.
	/// </summary>
	/// <param name="e">Details about the launch request and process.</param>
	protected override void OnLaunched(LaunchActivatedEventArgs e)
	{
		if (_container == null)
		{
			var logger = CreateLogger();
			_container = BuildContainer(logger);
		}

		// Do not repeat app initialization when the Window already has content,
		// just ensure that the window is active
		if (!(Window.Current.Content is Shell rootFrame))
		{
			// Create a Frame to act as the navigation context and navigate to the first page
			rootFrame = _container.Resolve<Shell>();
			rootFrame.DataContext = _container.Resolve<ShellViewModel>();

			if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
			{
				//TODO: Load state from previously suspended application
			}

			// Place the frame in the current Window
			Window.Current.Content = rootFrame;
		}

		if (e.PrelaunchActivated == false)
		{
			if (rootFrame.DataContext is ShellViewModel shellViewModel)
			{
				_ = shellViewModel.OnNavigatedToAsync(null);
			}

			// Ensure the current window is active
			Window.Current.Activate();
		}
	}

	private static IContainer BuildContainer(ILogger logger)
	{
		var applicationConfiguration = new ApplicationConfiguration();
		var containerBuilder = new ContainerBuilder();

		containerBuilder.RegisterInstance(applicationConfiguration).AsImplementedInterfaces();
		containerBuilder.RegisterLogger(logger);
		containerBuilder.RegisterAssemblyModules(typeof(App).Assembly);

		return containerBuilder.Build();
	}

	private static ILogger CreateLogger()
	{
		var configuration = new LoggerConfiguration()
			.Enrich.FromLogContext()
			.WriteTo.Debug();

		return configuration.CreateLogger();
	}

	/// <summary>
	/// Invoked when application execution is being suspended.  Application state is saved
	/// without knowing whether the application will be terminated or resumed with the contents
	/// of memory still intact.
	/// </summary>
	/// <param name="sender">The source of the suspend request.</param>
	/// <param name="e">Details about the suspend request.</param>
	private void OnSuspending(object sender, SuspendingEventArgs e)
	{
		var deferral = e.SuspendingOperation.GetDeferral();
		//TODO: Save application state and stop any background activity
		deferral.Complete();
	}
}