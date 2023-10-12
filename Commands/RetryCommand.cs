using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkribeSeinSDBot.MatrixBotSdk;
using static System.Net.Mime.MediaTypeNames;
using static SkribeSeinSDBot.Extensions;

namespace SkribeSeinSDBot.Commands
{
	internal class RetryCommand : ICommand
	{
		private readonly MatrixBot _matrixBot;

		public RetryCommand(MatrixBot matrixBot)
		{
			_matrixBot = matrixBot;
		}

		public string Name { get; } = "Retry";

		public string HelpText
		{
			get
			{
				return "retry: draw me: [any option from generation overwriting old]";
			}
		}

		public bool Test(string input, MatrixBotEventArgs botEventArgs)
		{
			if (input.Trim().StartsWith("retry:", StringComparison.InvariantCultureIgnoreCase))
			{
				var cmdText = input.Trim()["retry:".Length..];
				return true;
				var values = StableDiffusionGeneratorCmd.ReadInput(cmdText);
				return values is not null;
			}

			return false;
		}

		public IDictionary<string, InputData> UserCommands { get; set; } = new Dictionary<string, InputData>();

		public async Task Run(string input, MatrixBotEventArgs botEventArgs)
		{
			if (!UserCommands.TryGetValue(botEventArgs.Event.Sender, out var baseCommand))
			{
				await _matrixBot.PostThreadMessage("", $"Sorry, i can not {Spite("retry", "erneut versuchen")}. There is not old {Spite("command", "ANWEISUNG")} to pick from.", botEventArgs).ConfigureAwait(false);
				return;
			}

			var cmdText = input.Trim()["retry:".Length..];
			var values = StableDiffusionGeneratorCmd.ReadInput(cmdText, baseCommand);
			var stableDiffusionGeneratorCmd = Config._instance.Commands.OfType<StableDiffusionGeneratorCmd>().FirstOrDefault();
			await _matrixBot.PostThreadMessage("", $"Sure boss, {Spite("will retry using this settings", "Those are older settings but they check out")}: {values}", botEventArgs).ConfigureAwait(false);
			stableDiffusionGeneratorCmd.ProcessQueue.Enqueue((new ProcessItem(botEventArgs.Event.Sender, cmdText, DateTime.UtcNow, values, botEventArgs)));
		}
	}
}
