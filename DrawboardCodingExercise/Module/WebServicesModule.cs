using Autofac;
using DrawboardCodingExercise.Services;
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
	protected override void Load(ContainerBuilder builder)
	{
		var jsonSettings = new JsonSerializerSettings
		{
			ContractResolver = new CamelCasePropertyNamesContractResolver(),
			Formatting = Formatting.Indented
		};

		builder.RegisterInstance(jsonSettings).AsSelf();
		builder.RegisterType<APIClient>().As<IAPIClient>();
	}
}