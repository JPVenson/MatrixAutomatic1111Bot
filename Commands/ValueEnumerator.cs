using System.Text.RegularExpressions;

namespace SkribeSeinSDBot.Commands;

public class ValueEnumerator
{
	public ValueEnumerator()
	{
	}

	public Regex Regex { get; set; }
	public string Name { get; set; }
	public string HelpText { get; set; }
	public Action<Match, InputData> Action { get; set; }

	public string? Default { get; set; }

	public static ValueEnumerator Build()
	{
		return new ValueEnumerator();
	}

	public ValueEnumerator WithRegex(Regex regex)
	{
		Regex = regex;
		return this;
	}

	public ValueEnumerator WithDefault(string defaultValue)
	{
		Default = defaultValue;
		return this;
	}

	public ValueEnumerator WithName(string name)
	{
		Name = name;
		return this;
	}

	public ValueEnumerator WithHelpText(string helpText)
	{
		HelpText = helpText;
		return this;
	}

	public ValueEnumerator WithAction(Action<Match, InputData> action)
	{
		Action = action;
		return this;
	}
}