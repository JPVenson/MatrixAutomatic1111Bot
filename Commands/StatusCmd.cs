using System;
using System.Text;
using SkribeSeinSDBot.MatrixBotSdk;

namespace SkribeSeinSDBot.Commands;

public class StatusCmd : ICommand
{
	private readonly MatrixBotSdk.MatrixBot _matrixBot;

	public StatusCmd(MatrixBotSdk.MatrixBot matrixBot)
	{
		_matrixBot = matrixBot;
	}

	public string Name { get; } = "status";
	public string HelpText { get; } = "<code>status</code>";

	public bool Test(string input, MatrixBotEventArgs botEventArgs)
	{
		return input.Trim().ToLower() == "status";
	}

	public async Task Run(string input, MatrixBotEventArgs botEventArgs)
	{
		var stableDiffusionGeneratorCmd =
			Config._instance.Commands.OfType<StableDiffusionGeneratorCmd>().FirstOrDefault();
		var currentWork = stableDiffusionGeneratorCmd.CurrentWork;
		if (stableDiffusionGeneratorCmd.ProcessQueue.Count == 0 && currentWork == default)
		{
			await _matrixBot.PostThreadMessage("",
				"Nothin to do, and you?", botEventArgs).ConfigureAwait(false);
		}
		else
		{
			var sb = new StringBuilder()
				.Append("Currently doing stuff for")
			.AppendLine($"<code>{currentWork.User}</code> wants <code>{currentWork.Prompt}</code> at <code>{currentWork.StartDate}</code>")
			.AppendLine("<ol>");
			
			var processQueue = stableDiffusionGeneratorCmd.ProcessQueue.Where(e => !e.Cancelled).ToArray();
			for (var index = 0; index < processQueue.Length; index++)
			{
				var valueTuple = processQueue[index];
				sb.AppendLine($"<li>\t <code>{valueTuple.User}</code> wants <code>{valueTuple.Prompt}</code> at <code>{valueTuple.StartDate}</code></li>");
			}

			var text = sb.AppendLine("</ol>").ToString();

			await _matrixBot.PostThreadMessage("", text, botEventArgs).ConfigureAwait(false);
		}
	}
}