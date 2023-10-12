using System.Text.RegularExpressions;
using SkribeSeinSDBot.MatrixBotSdk;

namespace SkribeSeinSDBot.Commands;

public class CancelCommand : ICommand
{
	private readonly MatrixBotSdk.MatrixBot _matrixBot;
	public static Regex CancelCommandRegex = new Regex("cancel \\#(?<cancel>\\d*)");

	public CancelCommand(MatrixBot matrixBot)
	{
		_matrixBot = matrixBot;
	}

	public string Name { get; } = "Cancel";
	public string HelpText { get; } = "cancel <code>#NO</code> or <code>all</code>";

	public bool Test(string input, MatrixBotEventArgs botEventArgs)
	{
		return CancelCommandRegex.IsMatch(input);
	}

	public async Task Run(string input, MatrixBotEventArgs botEventArgs)
	{
		var group = CancelCommandRegex.Match(input).Groups["cancel"];
		if (int.TryParse(group.ValueSpan, out var groupNo))
		{
			var stableDiffusionGeneratorCmd =
				Config._instance.Commands.OfType<StableDiffusionGeneratorCmd>().FirstOrDefault();
			var toCancel = stableDiffusionGeneratorCmd.ProcessQueue.ToArray().ElementAt(groupNo);
			if (toCancel.User == botEventArgs.Event.Sender)
			{
				toCancel.Cancelled = true;
				await _matrixBot.PostThreadMessage("", $"Cancelled {toCancel.InputData}", botEventArgs).ConfigureAwait(false);
			}
			else
			{
				await _matrixBot.PostThreadMessage("", "Thats a nooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo.", botEventArgs).ConfigureAwait(false);
			}
		}
		else
		{
			if (group.Value.Equals("all", StringComparison.InvariantCultureIgnoreCase))
			{

			}
			else
			{
				await _matrixBot.PostThreadMessage("", "cant even type a number hä?", botEventArgs).ConfigureAwait(false);
			}
		}
	}
}