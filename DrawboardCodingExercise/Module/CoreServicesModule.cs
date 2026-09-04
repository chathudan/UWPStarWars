using Autofac;
using DrawboardCodingExercise.Contracts.CoreFramework;
using DrawboardCodingExercise.Contracts.Services;
using DrawboardCodingExercise.CoreFramework;
using DrawboardCodingExercise.Services.EventAggregator;
using JetBrains.Annotations;

namespace DrawboardCodingExercise.Module;

/// <summary>
/// Defines the services that make up the Core Framework.
/// </summary>
[UsedImplicitly]
public class CoreServicesModule : Autofac.Module
{
	protected override void Load(ContainerBuilder builder)
	{
		builder.RegisterType<NavigationService>().As<INavigationService, IFrameNavigator>().SingleInstance();
		builder.RegisterType<ThreadDispatcher>().As<IThreadDispatcher>();

		builder.RegisterType<EventAggregator>().As<IEventAggregator>().SingleInstance();
		builder.RegisterType<UserInteractionService>().As<IUserInteractionService>();
		builder.RegisterType<LocalizationService>().As<ILocalizationService>();
	}
}