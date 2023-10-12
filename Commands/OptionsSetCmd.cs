using SkribeSeinSDBot.MatrixBotSdk;
using System.Security;
using System.Text;
using static SkribeSeinSDBot.Extensions;

namespace SkribeSeinSDBot.Commands;

public class OptionsSetCmd : ICommand
{
	private readonly MatrixBotSdk.MatrixBot _matrixBot;

	public OptionsSetCmd(MatrixBot matrixBot)
	{
		_matrixBot = matrixBot;
	}

	public string Name { get; } = "Generate";

	public string HelpText
	{
		get
		{
			var helpBuilder = new StringBuilder();
			helpBuilder.AppendLine("Set default values.")
				.AppendLine("<ol>");
			foreach (var valueEnumerator in StableDiffusionGeneratorCmd.Values)
			{
				helpBuilder.AppendLine($"<li>{valueEnumerator.Name}: <code>set option: {valueEnumerator.Default}</code></li>");
			}

			helpBuilder.AppendLine("</ol>");
			return helpBuilder.ToString();
		}
	}

	public bool Test(string input, MatrixBotEventArgs botEventArgs)
	{
		var optionCmd = "set option:";
		var trim = input.Trim();
		if (trim.StartsWith(optionCmd, StringComparison.InvariantCultureIgnoreCase))
		{
			var text = trim.Substring(optionCmd.Length).Trim();
			var option = StableDiffusionGeneratorCmd.Values.FirstOrDefault(e => e.Regex.IsMatch(text));
			return option != null;
		}
		return false;
	}

	public async Task Run(string input, MatrixBotEventArgs botEventArgs)
	{
		var optionCmd = "set option:";
		var text = input.Trim().Substring(optionCmd.Length).Trim();
		var option = StableDiffusionGeneratorCmd.Values.FirstOrDefault(e => e.Regex.IsMatch(text));

		if (Config._instance.ConfigService.ConfigStore.LockedSettings.Contains(option.Name) && !Config._instance.CheckAdminUser(botEventArgs.Event.Sender))
		{
			await _matrixBot.PostThreadMessage("", $"Nope. {Spite(Spite("PERMISSION DENIED", "HELL NAW, YOU HAVE NO POWER HERE!!"), Spite("DU SAU KANNST MIR GARNICHTS!", "⚠️🚫☢️NEIN!! GENEHMIGUNG VERWEIGERT!!!☢️🚫⚠️\r\n"))}!", botEventArgs).ConfigureAwait(false);
			return;
		}

		if (option.Default == text)
		{
			await _matrixBot.PostThreadMessage("", $"Are you drunk?. option <code>{option.Name}</code> is already default to <code>{option.Default}</code>", botEventArgs).ConfigureAwait(false);
		}
		else
		{
			option.Default = text;
			Config._instance.ConfigService.ConfigStore.Defaults[option.Name] = text;
			await Config._instance.ConfigService.Save().ConfigureAwait(false);
			await _matrixBot.PostThreadMessage("", $"Sure thing. I updated option <code>{option.Name}</code> to default to <code>{option.Default}</code>", botEventArgs).ConfigureAwait(false);
		}
	}
}