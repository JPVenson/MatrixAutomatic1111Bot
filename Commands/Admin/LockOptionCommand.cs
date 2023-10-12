using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkribeSeinSDBot.MatrixBotSdk;

namespace SkribeSeinSDBot.Commands.Admin
{
	public class LockOptionCommand : ICommand
	{
		private readonly MatrixBot _matrixBot;

		public LockOptionCommand(MatrixBot matrixBot)
		{
			_matrixBot = matrixBot;
		}

		public string Name { get; } = "Admin: Lock option";
		public string HelpText { get; } = "<code>toggle lock option: Size</code>";

		const string _toggleLockOption = "toggle lock option:";
		public bool Test(string input, MatrixBotEventArgs botEventArgs)
		{
			return input.Trim().StartsWith(_toggleLockOption, StringComparison.CurrentCultureIgnoreCase) && Config._instance.CheckAdminUser(botEventArgs.Event.Sender);
		}

		public async Task Run(string input, MatrixBotEventArgs botEventArgs)
		{
			var optionName = input[(_toggleLockOption.Length + 1)..].Trim();
			if (Config._instance.ConfigService.ConfigStore.LockedSettings.Contains(optionName))
			{
				Config._instance.ConfigService.ConfigStore.LockedSettings.Remove(optionName);
				await _matrixBot.PostThreadMessage("", $"Oki Doki i <b>unlocked</b> \"{optionName}\"", botEventArgs).ConfigureAwait(false);
			}
			else
			{
				Config._instance.ConfigService.ConfigStore.LockedSettings.Add(optionName);
				await _matrixBot.PostThreadMessage("", $"Oki Doki i <b>locked</b> \"{optionName}\"", botEventArgs).ConfigureAwait(false);
			}
			
			await Config._instance.ConfigService.Save().ConfigureAwait(false);
		}
	}
}
