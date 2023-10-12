using SkribeSeinSDBot.MatrixBotSdk;

namespace SkribeSeinSDBot.Commands;

public record ProcessItem
{
	public ProcessItem(string user, string prompt, DateTime startDate, InputData inputData, MatrixBotEventArgs args)
	{
		User = user;
		Prompt = prompt;
		StartDate = startDate;
		InputData = inputData;
		Args = args;
	}

	public string User { get; set; }
	public string Prompt { get; set; }
	public DateTime StartDate { get; set; }
	public InputData InputData { get; set; }
	public MatrixBotEventArgs Args { get; set; }
	public bool Cancelled { get; set; }
}