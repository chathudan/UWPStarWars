namespace DrawboardCodingExercise.ViewModel.Models;

/// <summary>One row in a film's character section: a name, and a stable key for the list.</summary>
public sealed class CharacterListItem
{
	public CharacterListItem(string name, string url)
	{
		Name = name;
		Url = url;
	}

	public string Name { get; }
	public string Url { get; }
}
