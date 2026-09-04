using System;
using Autofac;
using CommunityToolkit.Mvvm.ComponentModel;
using DrawboardCodingExercise.Contracts;
using JetBrains.Annotations;

namespace DrawboardCodingExercise.Module;

/// <summary>
/// Utility extension methods to make registering View/ViewModel pairs easier.
/// </summary>
[UsedImplicitly]
public static class MvvmViewExtensions
{
	public static void RegisterView<TView, TViewModel>(this ContainerBuilder builder, PageKey pageKey)
	{
		builder.RegisterType(typeof(TViewModel)).Keyed<ObservableObject>(pageKey);
		builder.RegisterInstance(typeof(TView)).Keyed<Type>(pageKey);
	}
}