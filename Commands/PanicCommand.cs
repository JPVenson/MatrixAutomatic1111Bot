using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Memory;
using SkribeSeinSDBot.MatrixBotSdk;

namespace SkribeSeinSDBot.Commands
{
	internal class PanicCommand : ICommand
	{
		private readonly MatrixBot _matrixBot;

		public PanicCommand(MatrixBot matrixBot)
		{
			_matrixBot = matrixBot;
			MatrixBotJsonSends = new Dictionary<string, Stack<MatrixBotJsonSend>>();
		}

		public string Name { get; } = "PANIC";
		public string HelpText { get; } = "panic x1. \r\n removes X recently removed images.";

		private static Regex _panic = new Regex(@"panic x(\d*)", RegexOptions.IgnoreCase);
		public bool Test(string input, MatrixBotEventArgs botEventArgs)
		{
			var match = _panic.Match(input);
			return match.Success && int.TryParse(match.Groups[1].ValueSpan, out _);
		}

		public IDictionary<string, Stack<MatrixBotJsonSend>> MatrixBotJsonSends { get; set; }

		public void AddImage(MatrixBotEventArgs botEventArgs, MatrixBotJsonSend matrixBotJsonSend)
		{
			if (!MatrixBotJsonSends.TryGetValue(botEventArgs.Event.Sender, out var imageList))
			{
				imageList = new Stack<MatrixBotJsonSend>();
				MatrixBotJsonSends[botEventArgs.Event.Sender] = imageList;
			}

			if (imageList.Count > 20)
			{
				imageList.Pop();
			}
			imageList.Push(matrixBotJsonSend);
		}

		public async Task Run(string input, MatrixBotEventArgs botEventArgs)
		{
			if (MatrixBotJsonSends.TryGetValue(botEventArgs.Event.Sender, out var imageList))
			{
				var panics = int.Parse(_panic.Match(input).Groups[1].ValueSpan);
				for (int i = 0; i < panics; i++)
				{
					if (imageList.TryPop(out var img))
					{
						await _matrixBot.UpdateMessage("* Redacted", botEventArgs, img).ConfigureAwait(false);
					}
				}
			}
		}
	}
}
