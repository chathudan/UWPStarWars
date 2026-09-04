using DrawboardCodingExercise.Contracts;

namespace DrawboardCodingExercise.CoreFramework;

/// <summary>
/// Houses an instance of a navigation and acts as the parameter to a View.
/// </summary>
internal class NavigationDetails
{
	public NavigationDetails(PageKey pageKey, object parameter)
	{
		PageKey = pageKey;
		Parameter = parameter;
	}

	public PageKey PageKey { get; }
	public object Parameter { get; }

	public override bool Equals(object obj)
	{
		if (ReferenceEquals(null, obj))
		{
			return false;
		}

		if (ReferenceEquals(this, obj))
		{
			return true;
		}

		if (obj.GetType() != GetType())
		{
			return false;
		}

		return Equals((NavigationDetails) obj);
	}

	private bool Equals(NavigationDetails other)
	{
		return PageKey == other.PageKey && Equals(Parameter, other.Parameter);
	}

	public override int GetHashCode()
	{
		unchecked
		{
			return ((int) PageKey * 397) ^ (Parameter != null ? Parameter.GetHashCode() : 0);
		}
	}

	public static bool operator ==(NavigationDetails left, NavigationDetails right)
	{
		return Equals(left, right);
	}

	public static bool operator !=(NavigationDetails left, NavigationDetails right)
	{
		return !Equals(left, right);
	}
}