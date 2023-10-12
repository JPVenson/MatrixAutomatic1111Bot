using System.Collections.Concurrent;
using System.Reflection;
using SkribeSeinSDBot.Commands;
using SkribeSeinSDBot.MatrixBotSdk;
using SkribeSeinSDBot.Store;
using MatrixBotEventArgs = SkribeSeinSDBot.MatrixBotSdk.MatrixBotEventArgs;
using static SkribeSeinSDBot.Extensions;


Console.WriteLine("Skribe sein Matrix bot <3");

Console.Write("StableDiffusionUrl: ");
Config._instance.StableDiffusionServer = Config.GetInputImpl("SdPath");

var configStoreService = new ConfigStoreService();
Config._instance.ConfigService = configStoreService;
await Config._instance.ConfigService.Load().ConfigureAwait(false);
Config._instance.Botname = Config._instance.ConfigService.ConfigStore.BotName;
if (Config._instance.ConfigService.ConfigStore.BotName is null or "")
{
	Console.WriteLine("Non configured BotName in config.json");
	return;
}


var matrixBot = new SkribeSeinSDBot.MatrixBotSdk.MatrixBot(null, null, $"[{Config._instance.ConfigService.ConfigStore.BotName}]");


var types = typeof(Program).Assembly.GetTypes().Where(e => !e.IsInterface)
	.Where(e => typeof(ICommand).IsAssignableFrom(e));
Config._instance.Commands = types.Select(type => Activator.CreateInstance(type, new object[]
	{
		matrixBot
	}))
	.Select(e => e as ICommand)
	.ToArray();

BlockingCollection<MatrixBotEventArgs> _workerQueue = new BlockingCollection<MatrixBotEventArgs>();

matrixBot.OnEvent += (object? sender, MatrixBotEventArgs e) =>
{
	try
	{
		var contentBody = e.Event.Content?.Body;
		Console.WriteLine($"{e.RoomId} : {e.Event.Sender} : {contentBody}");
		if (Config._instance.ConfigService.ConfigStore.RoomsEnabled.Contains(e.RoomId)
		    && contentBody?.StartsWith($"{Config._instance.Botname}:", StringComparison.InvariantCultureIgnoreCase) == true)
		{
			_workerQueue.Add(e);
		}
	}
	catch (Exception exception)
	{
		
	}
};

async Task matrixBotOnOnEvent(MatrixBotEventArgs matrixBotEventArgs, MatrixBot matrixBot1)
{
	var text = matrixBotEventArgs.Event.Content?.Body?[(Config._instance.Botname.Length + 1)..];
	if (text is null)
	{
		return;
	}

	var cmd = Config._instance.Commands.FirstOrDefault(c => c.Test(text, matrixBotEventArgs));
	if (cmd != null)
	{
		await cmd.Run(text, matrixBotEventArgs).ConfigureAwait(false);
	}
	else
	{
		await matrixBot1.PostRoomMessage(matrixBotEventArgs.RoomId,
			"", $"Sory {matrixBotEventArgs.Event.Sender} i did not get that").ConfigureAwait(false);
	}
}

var worker = new Task[1];
for (int i = 0; i < worker.Length; i++)
{
	worker[i] = Task.Factory.StartNew(async () =>
	{
		foreach (var itemToWork in _workerQueue.GetConsumingEnumerable())
		{
			await matrixBotOnOnEvent(itemToWork, matrixBot).ConfigureAwait(false);
		}
	}, TaskCreationOptions.LongRunning);
}

try
{
	matrixBot.Start();
	foreach (var room in Config._instance.ConfigService.ConfigStore.RoomsEnabled)
	{
		await matrixBot.PostRoomMessage(room,
			"",
			$"{Spite("Hiiii", "UwU")} i am {Config._instance.ConfigService.ConfigStore.BotName} and i am ready to accept commands.").ConfigureAwait(false);
	}

	Console.ReadLine();
}
catch (Exception e)
{
	foreach (var room in Config._instance.ConfigService.ConfigStore.RoomsEnabled)
	{
		await matrixBot.PostRoomMessage(room,
			"", $"Wupsiiii. I just crashed. START ME AGAIN SLAVES!").ConfigureAwait(false);
	}
}
finally
{
	foreach (var room in Config._instance.ConfigService.ConfigStore.RoomsEnabled)
	{
		await matrixBot.PostRoomMessage(room,
			"", $"Good night. Love {Config._instance.ConfigService.ConfigStore.BotName}").ConfigureAwait(false);
	}
}

public class Config
{
	public static Config _instance = new Config();

	public string StableDiffusionServer { get; set; }
	public string Botname { get; set; }
	public ICommand[] Commands { get; set; }
	public ConfigStoreService ConfigService { get; set; }


	protected virtual string GetInput(string name)
	{
		return GetInputImpl(name);
	}

	public static string GetInputImpl(string name)
	{
		var sourceDir = Environment.GetEnvironmentVariable(name);
		if (!string.IsNullOrWhiteSpace(sourceDir))
		{
			Console.WriteLine($"[From Environment] {sourceDir}");
			return sourceDir;
		}
		else
		{
			var cmdArg = Environment.GetCommandLineArgs()
				.FirstOrDefault(e => e.StartsWith(name + "="));
			if (cmdArg != null)
			{
				var cmd = cmdArg[(name.Length + 1)..];
				Console.WriteLine($"[From Commandline] {cmd}");
				return cmd;
			}
			else
			{
				return Console.ReadLine();
			}
		}
	}

	public bool CheckAdminUser(string eventSender)
	{
		return ConfigService.ConfigStore.AdminUsers.Contains(eventSender);
	}
}