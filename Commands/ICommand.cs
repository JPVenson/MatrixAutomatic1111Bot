using SkribeSeinSDBot.MatrixBotSdk;

namespace SkribeSeinSDBot.Commands;

public interface ICommand
{
	string Name { get; }
	string HelpText { get; }
	bool Test(string input, MatrixBotEventArgs botEventArgs);
	Task Run(string input, MatrixBotEventArgs botEventArgs);
}