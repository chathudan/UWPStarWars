using System;
using Autofac;
using DrawboardCodingExercise.Services;
using DrawboardCodingExercise.Services.StarWars;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DrawboardCodingExercise.Module;

/// <summary>
/// Defines the services that talk to the Drawboard API.
/// </summary>
[UsedImplicitly]
public class WebServicesModule : Autofac.Module
{
	// FR-015: a single request is abandoned after this long and treated as a recoverable
	// failure. Tests inject their own millisecond-scale budget - this is the real, production value.
	private static readonly TimeSpan StarWarsRequestBudget = TimeSpan.FromSeconds(15);

	protected override void Load(ContainerBuilder builder)
	{
		var jsonSettings = new JsonSerializerSettings
		{
			ContractResolver = new CamelCasePropertyNamesContractResolver(),
			Formatting = Formatting.Indented
		};

		builder.RegisterInstance(jsonSettings).AsSelf();
		builder.RegisterType<APIClient>().As<IAPIClient>();

		builder.RegisterType<StarWarsService>()
			.As<IStarWarsService>()
			.WithParameter(TypedParameter.From(StarWarsRequestBudget));
	}
}