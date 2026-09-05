namespace DrawboardCodingExercise.ViewModel;

/// <summary>
/// The mutually exclusive condition of a data-bearing region at any moment, determining what
/// the user sees. Error and InvalidSelection are distinct: only Error offers a retry - retrying
/// an id the source does not recognise can never succeed (FR-013).
/// </summary>
public enum PageLoadState
{
	Loading,
	Loaded,
	Empty,
	Error,
	InvalidSelection,

	/// <summary>
	/// Nothing has been requested yet - the condition a collapsed related-category section is in
	/// before the user first expands it (FR-028). Added LAST on purpose: it must not become
	/// default(PageLoadState), which the pages rely on being Loading.
	/// </summary>
	NotStarted
}
