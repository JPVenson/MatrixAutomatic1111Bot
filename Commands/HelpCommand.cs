using System.Data.SqlTypes;
using System.Reflection;
using System.Text;
using SkribeSeinSDBot.MatrixBotSdk;

namespace SkribeSeinSDBot.Commands;

public class HelpCommand : ICommand
{
	private readonly MatrixBotSdk.MatrixBot _matrixBot;

	public HelpCommand(MatrixBot matrixBot)
	{
		_matrixBot = matrixBot;
	}

	public string Name { get; } = "Help";
	public string HelpText { get; } = "help";

	public bool Test(string input, MatrixBotEventArgs botEventArgs)
	{
		return input.Trim().Equals("help", StringComparison.InvariantCultureIgnoreCase);
	}

	public async Task Run(string input, MatrixBotEventArgs botEventArgs)
	{
		var sb = new StringBuilder();
		sb.Append("Help:<br/>");
		foreach (var cmd in Config._instance.Commands)
		{
			sb.Append(cmd.Name)
				.Append(" : ")
				.Append(Config._instance.Botname)
				.Append(": ")
				.AppendLine(cmd.HelpText)
				.AppendLine("<br> ------------------------------------- <br>")
				.AppendLine()
				.AppendLine()
				.AppendLine();
		}

		await _matrixBot.PostThreadMessage(sb.ToString(), sb.ToString(), botEventArgs).ConfigureAwait(false);
	}
}