# UWP Coding Exercise

## Overview

Welcome to the Drawboard UWP coding exercise. This exercise is indicative of the kind of work we do at Drawboard and allows us to understand how you approach and resolve some common programming challenges.

The coding exercise includes a number of libraries that we use in our applications, for more information check the [dependencies](#Dependencies) section.

<br/>

## Getting Started

In order develop and run the coding exercise, we recommend using Visual Studio 2026 (18.7+) with the following workloads and components:

* Workloads
	* Windows application development
	* .NET desktop development
* Additional Components
	* Universal Windows Platform tools
	* Windows 11 SDK (10.0.26100.0)

## The task

Modify the given UWP interview test app to retrieve and display data from a public API. 
There are two options for which public API to use for this interview test. (We give two options, just in case one of the APIs are down)
1. Star Wars Movies API - https://swapi.info/
1. The Metropolitan Museum of Art (MET) Collection API - https://metmuseum.github.io/

The task will involve creating two pages of content. The first page displaying an initial list, and the second page providing a detailed view of a selected list item in page 1. 

You are welcome to pull in whatever nuget packages you think are appropriate, but we ask that you don't pull in any libraries that talk directly to the APIs in question. (The apps we write at drawboard talk directly to our own REST APIs, so we would like to see you do that as well)

<br/>

## What we are looking for

We would like the program you provide to extend the provided UWP application. We are interested in seeing how you modify an existing codebase. If you are unable to work with the existing application, you are welcome to submit a brand new application that achieves the required task.

The kinds of things we will be looking for when we evaluate your submission include:

* Solid design principles.
* Clean, well-factored, code.
* An architecture that is testable and extensible.
* Your project should contain automated tests
* Error checking and reporting to the user.
* Usability for the user.
* Code structure and adherence to current UWP idioms.
* You do not need to provide any form of persistence in this app, but you can if it helps your solution.
* You may use an AI Agent to help with the coding exercise. 
  * In this case, we will be interested in the challenges the agent faced and how you overcame them collaboratively.
  * We will also be interested in how you validated the code produced by the agent.

Please provide a README.md (or equivalent) in your code that describes any limitations with your solution, how you might extend it further, and any important considerations would be appreciated.

We will be referring to your solution as a starting point for discussion in the interview, so please be prepared to discuss your solution and the decisions you made.

<br/>

### Option 1 - Star Wars Movies API
https://swapi.info/

The star wars API provides information about the star wars movies.

<br/>

#### Page 1
On this page present a list of all films returned from the films endpoint (https://swapi.info/api/films).
For each film, display the title, and the episode number of the film.

<br/>

#### Page 2
This page should be navigated to when one of the films presented in page 1 is clicked on.
This page should contain details about the film that is clicked. 
The following details should be included:
- Title
- Episode Number
- Release Date
- Director
- Producer

The page should contain a list of one the following categories:
- Characters
- Planets
- Starships
- Vehicles
- Species

<br/>

#### Bonus Points
The films endpoint, returns the opening crawl for each of the films. Include this crawl on page 2. 

<br/>

### Option 2 - The Metropolitan Museum of Art (MET) Collection API 
https://metmuseum.github.io/

The MET APIs provides information about all the art pieces on display and the Metropoliton Musuem of Art. 

<br/>

#### Page 1
On this page present a list of departments from the following API (https://metmuseum.github.io/#departments).
For each department, list out the display name for the department. 

<br/>

#### Page 2
This page should be navigated to when one of the departments from page 1 is clicked (https://collectionapi.metmuseum.org/public/collection/v1/objects?).
This page should display a list of objects that belong to that department.
For simplicity you may like to only display the first 50 objects returned from the API. 
For each object, display the following bits of information:
- Title
- Culture
- Period
- A thumbnail of each piece of art

<br/>

#### Bonus Points
Provide a way to display all of the items in the list and not just the first 50.

<br/>

## Submitting your solution
Once you are ready to submit a solution, please send a zip file with your solution in it to hiring@drawboard.com

# Dependencies

The Drawboard Coding Exercise uses and provides a number of the same packages that we use in Drawboard PDF and/or Drawboard Projects.

* Autofac
	* Inversion of Control and Dependency Injection
* xUnit
	* Unit Testing Framework
* Shouldly
	* A fluent assertion library for unit tests
* Serilog
	* Logging library
* Reactive Extensions
	* A library for composing asynchronous and event-based programs using observable sequences
	* (Not used in this project, but available for your use if desired)
* PolySharp
	* Provides newer types and methods for UWP that are not available in the .net runtime
	* More information provided below
* CommunityToolkit.MVVM
	* provides source generators to reduce the amount of boilerplate code required for MVVM
	* More information provided below

You are welcome to bring in your own packages if you feel they would help, here are some additional ones we use that are not installed by default:
* WinUI 2.8+
* Community Toolkit
	* Provides a number of UI controls
* Riok.Mapperly

## PolySharp

As at the time of writing, UWP's .net runtime is currently stuck at a level of rougly .net core 2.1. This means that while we can use a lot of the newer C# language features, we can't use anything that requires new types or methods that are only available in newer versions of the .net runtime.

PolySharp is a library that provides a lot of these newer types and methods for UWP, such as the [IsExternalInitAttribute], which is required for record support.

PolySharp is a source generator that injects the types into your code (as opposed to supplying them as a library). 

In this project, we expose all the types publically so that records, ranges, etc, can be used from all of the projects.

For more information on PolySharp, see the [GitHub repository](https://github.com/Sergio0694/PolySharp)

## CommunityToolkit.MVVM

We use the CommunityToolkit.MVVM package to simplify the MVVM pattern. It uses source generators to simplify the boilerplate code required for implementing INotifyPropertyChanged and ICommand.

A brief example of using it will be provided below. For more information on the CommunityToolkit.MVVM package, see the [official documentation](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)

In order for it to work, a ViewModel must inherit from `ObservableObject` and be a partial class.

### INotifyPropertyChanged

If you're used to standard INotifyProperty Changed, you're probably used to this pattern:

```csharp
public class MyViewModel : ViewModel //(Where ViewModel implements INotifyPropertyChanged)
{
	private string _myProperty;
	public string MyProperty
	{
		get => _myProperty;
		set
		{
			if (_myProperty != value) {
				_myProperty = value;
				OnPropertyChanged(nameof(MyProperty));
				OnPropertyChanged(nameof(MyCalculatedProperty));

				//Additional logic
			}
		}
	}

	public string MyCalculatedProperty => $"{MyProperty} is the value of MyProperty";
}
```

With CommunityToolkit.MVVM, you can simplify this to:

```csharp
public partial class MyViewModel : ObservableObject

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(MyCalcualtedProperty))]
	private string _myProperty;

	public string MyCalculatedProperty => $"{MyProperty} is the value of MyProperty";

	partial void OnMyPropertyChanged(string value)
	{
		//Additional logic
	}
}
```
The ObservableProperty attribute will kick off the source generator to generate the property for you. It also generates partial methods for OnMyPropertyChanging and OnMyPropertyChanged that you can override for any additional logic you need.

#### ICommand

If you're used to standard ICommand, you're probably used to this pattern (Or similar):
```csharp
public class MyViewModel : ViewModel { //(Where ViewModel implements INotifyPropertyChanged)
	public RelayCommand SaveCommand { get; }

	public MyViewModel()
	{
		SaveCommand = new RelayCommand(ExecuteSaveCommand, () => HasChanges);
	}

	private bool _haschanges;
	public bool HasChanges { 
		get => _haschanges; 
		set {
			if (_haschanges != value) {
				_haschanges = value;
				MyCommand.RaiseCanExecuteChanged();
			}
		}
	}

	private void ExecuteSaveCommand()
	{
		//Do something
	}
}
```

With CommunityToolkit.MVVM, you can simplify this to:

```csharp
public partial class MyViewModel : ObservableObject
{
	[ObservableProperty, RaiseCanExecuteChangedFor(nameof(SaveCommand))] 
	private bool _hasChanges;

	[RelayCommand(CanExecute = nameof(HasChanges)]
	private void OnSave()
	{
		//Do something
	}
}
```

the `SaveCommand` property will be generated by the [RelayCommand]. It also natively supports async/await.

# Included Services

There are a number of services that are provided in the application that are reflective of a few of the services that we use in our products.

## Navigation Service

The navigation service (`INavigationService`) provides the ability to navigate between pages in the application, and deals with the complexities of creating the XAML view, and attaching a 
ViewModel to it. If the ViewModel implements `INavigateToAware`, the navigation service will call the `OnNavigatedTo` method after the page is loaded.`

It is based on a Navigate-by-page-key mechanism, so if you wish to add more pages, you'll need to extend the PageKey enum.

The navigation service uses Autofac to resolve the pages, and the registration of pages happens in [NavigationModule.cs](DrawboardCodingExercise/Module/NavigationModule.cs).

To Navigate to a page, use `Task NavigateAsync(PageKey pageKey, object parameter = null);`, where `parameter` will be passed to `INavigateToAware.OnNavigatedTo`.

## Thread Dispatcher

The thread dispatcher (`IThreadDispatcher`) provides a unified way to run code on the UI. It provides access to the window's `Dispatcher` from
.net standard 2.0 code, and keeps objects more testable without needing the UI to be present.

## User Interaction Service

The user interaction service (`IUserInteractionService`) provides a way to show a Retry/Cancel interface to the user, and is recommended if an API call fails

## Event Aggregator

The event aggregator (`IEventAggregator`) provides decoupled in-app messaging between components.

It's intended use in the coding exercise is to provide a progress mechanism to let users know when something is happening.

`ShellViewModel` understands two events `NotifyBusyEvent(string)` and `NotifyDoneEvent(string)`. The string provided will be displayed in the UI and must be the same for the BusyEvent and the DoneEvent for the progress to be cleared.

## Localization Service

The localization service (`ILocalizationService`) provides a way to localize strings in the application. It allows you to provide a key and the localized text will be returned from [Resources.resw](DrawboardCodingExercise/Strings/en/Resources.resw). 

It supports string.Format parameters.

## API Client

The API client (`IAPIClient`) provides an unauthenticated api for calling web requests and deserializing them as JSON.

Any server responses that are not a success code will be thrown as exceptions.

```csharp
	Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request);
	Task<TResponse> GetAsync<TResponse>(string path);
	Task<Stream> GetImageAsync(string path);
```
