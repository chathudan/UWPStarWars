namespace DrawboardCodingExercise.ViewModel.Models;

/// <summary>
/// One row inside any related-category section: a name, and a stable key for the list. There is
/// no per-category row type because a planet row and a character row differ in nothing this
/// feature displays.
/// </summary>
public sealed class RelatedResourceListItem
{
	public RelatedResourceListItem(string name, string url)
	{
		Name = name;
		Url = url;
	}

	public string Name { get; }
	public string Url { get; }
}
