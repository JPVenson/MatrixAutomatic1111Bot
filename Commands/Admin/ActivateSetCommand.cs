using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkribeSeinSDBot.MatrixBotSdk;

namespace SkribeSeinSDBot.Commands.Admin
{
	internal class ActivateSetCommand : ICommand
	{
		private MatrixBot _matrixBot;

		public ActivateSetCommand(MatrixBot matrixBot)
		{
			_matrixBot = matrixBot;
		}

		public string Name { get; } = "Admin: Active Model Set";

		public string HelpText
		{
			get
			{
				var sb = new StringBuilder();
				sb.AppendLine($"<code>activate set: {Config._instance.ConfigService.ConfigStore.ActiveSetName}</code>")
					.AppendLine("Known Sets:")
					.AppendLine("<ol>")
					.AppendLine(string.Join("", Config._instance.ConfigService.ConfigStore.ModelSets.OrderBy(s => s.Key).Select(e =>
					{
						return $"<li>" +
						       $"<code>{e.Key}</code> \r\n" +
							   $"<ul>{string.Join("", e.Value.Models.OrderBy(s => s).Select(s => $"<li><code>{s}</code></li>"))}</ul>" +
						       $"</li>";
					})))
					.AppendLine("</ol>");
				return sb.ToString();
			}
		}

		private const string _commandName = "activate set:";
		public bool Test(string input, MatrixBotEventArgs botEventArgs)
		{
			return input.Trim().StartsWith(_commandName) && Config._instance.CheckAdminUser(botEventArgs.Event.Sender);
		}

		public async Task Run(string input, MatrixBotEventArgs botEventArgs)
		{
			var setName = input[(_commandName.Length + 1)..].Trim();
			if (Config._instance.ConfigService.ConfigStore.ModelSets.TryGetValue(setName, out var set))
			{
				Config._instance.ConfigService.ConfigStore.ActiveSetName = setName;
				await Config._instance.ConfigService.Save().ConfigureAwait(false);

				await _matrixBot.PostThreadMessage("", $"Allrighty activated set \"{setName}\" with <ol>{string.Join("", set.Models.OrderBy(s => s).Select(e =>
				{
					return $"<li><code>{e}</code></li>";
				}))}</ol>", botEventArgs).ConfigureAwait(false);
			}
			else
			{
				await _matrixBot.PostThreadMessage("", $"nah dude, that set does not exist \"{setName}\"", botEventArgs).ConfigureAwait(false);
			}
		}
	}
}
