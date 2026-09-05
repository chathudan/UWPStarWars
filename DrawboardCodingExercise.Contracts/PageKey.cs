namespace DrawboardCodingExercise.Contracts;

public enum PageKey
{
	Welcome,
	PageA,

	/// <summary>Page 1 - the list of Star Wars films. The application's startup page.</summary>
	Films,

	/// <summary>Page 2 - details of a single film, navigated to with the film's opaque identifier.</summary>
	FilmDetails
}