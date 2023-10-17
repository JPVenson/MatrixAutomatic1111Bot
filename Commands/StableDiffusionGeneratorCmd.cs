using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using RestSharp;
using SkribeSeinSDBot.MatrixBotSdk;
using SkribeSeinSDBot.SdApiClient;
using MatrixBotEventArgs = SkribeSeinSDBot.MatrixBotSdk.MatrixBotEventArgs;
using static SkribeSeinSDBot.Extensions;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace SkribeSeinSDBot.Commands;

internal class StableDiffusionGeneratorCmd : ICommand
{
	private readonly MatrixBot _matrixBot;

	public ConcurrentQueue<ProcessItem> ProcessQueue { get; set; }

	public ProcessItem? CurrentWork { get; private set; }

	public StableDiffusionGeneratorCmd(MatrixBot matrixBot)
	{
		ProcessQueue =
			new();
		_matrixBot = matrixBot;
		StartWorker();
	}

	public static ValueEnumerator InputMain =
		ValueEnumerator.Build()
			.WithName("main")
			.WithHelpText(
				"Required prefix text. Like <code>draw me ([Optional]public): \"Positive Prompt\"</code> followed optionally by <code>but not \"Negative prompt\"</code>")
			.WithRegex(new(@"(?:(?:generate me)|(?:draw me)\s*(?<public>public)*\s*)(?:\:)*\s*(""|`){1}(?<pos>.*?)(?:\k<1>)(?: but not (""|`){1}(?<neg>.*?)(?:\k<2>))?",
				RegexOptions.Compiled | RegexOptions.IgnoreCase))
			.WithAction((e, f) =>
			{
				f.Positive = e.Groups["pos"].Value;
				f.Negative = e.Groups["neg"].Value;
				f.InPublic = e.Groups["public"].Value == "public";
			});

	public static IEnumerable<ValueEnumerator> Values = new ValueEnumerator[]
	{
		ValueEnumerator.Build()
			.WithName("Size")
			.WithHelpText("with size \"1024x1536\"")
			.WithDefault("with size \"1024x1536\"")
			.WithRegex(new(@"(?:with size (""|`){1}(?<size_x>.*?)x(?<size_y>.*?)(?:\k<1>))",
				RegexOptions.Compiled | RegexOptions.IgnoreCase))
			.WithAction((Match e, InputData f) =>
			{
				if (int.TryParse(e.Groups["size_x"].ValueSpan, out var sizeX))
				{
					f.SizeX = sizeX;
				}

				if (int.TryParse(e.Groups["size_y"].ValueSpan, out var sizeY))
				{
					f.SizeY = sizeY;
				}
			}),
		ValueEnumerator.Build()
			.WithName("Batchsize")
			.WithHelpText("in batch of \"5\"")
			.WithDefault("in batch of \"1\"")
			.WithRegex(new(@"(?:in batch of (""|`){1}(?<batch>.*?)(?:\k<1>))", RegexOptions.Compiled | RegexOptions.IgnoreCase))
			.WithAction((Match e, InputData f) =>
			{
				if (int.TryParse(e.Groups["batch"].ValueSpan, out var batch))
				{
					f.BatchSize = batch;
				}
			}),
		ValueEnumerator.Build()
			.WithName("Batch Count")
			.WithHelpText("repeat \"5\"")
			.WithDefault("repeat \"1\"")
			.WithRegex(new(@"(?:repeat (""|`){1}(?<batch>.*?)(?:\k<1>))", RegexOptions.Compiled | RegexOptions.IgnoreCase))
			.WithAction((Match e, InputData f) =>
			{
				if (int.TryParse(e.Groups["batch"].ValueSpan, out var batch))
				{
					f.BatchCount = batch;
				}
			}),
		ValueEnumerator.Build()
			.WithName("Steps")
			.WithHelpText("with steps \"50\"")
			.WithDefault("with steps \"50\"")
			.WithRegex(new(@"^(?:with steps (""|`){1}(?<steps>.*?)(?:\k<1>))$", RegexOptions.Compiled | RegexOptions.IgnoreCase))
			.WithAction((Match e, InputData f) =>
			{
				if (int.TryParse(e.Groups["steps"].ValueSpan, out var batch))
				{
					f.Steps = batch;
				}
			}),
		ValueEnumerator.Build()
			.WithName("Model")
			.WithHelpText("using \"crystalClearXL_ccxl\"")
			.WithDefault("using \"\"")
			.WithRegex(new(@"(?:using (""|`){1}(?<model>.*?)(?:\k<1>))", RegexOptions.Compiled | RegexOptions.IgnoreCase))
			.WithAction((Match e, InputData f) => f.Model = e.Groups["model"].Value),
		ValueEnumerator.Build()
			.WithName("Sampler")
			.WithHelpText("using sampler \"DPM 2M\"")
			.WithDefault("using sampler \"DPM 2M\"")
			.WithRegex(new(@"(?:using sampler (""|`){1}(?<sampler>.*?)(?:\k<1>))", RegexOptions.Compiled | RegexOptions.IgnoreCase))
			.WithAction((Match e, InputData f) => f.Sampler = e.Groups["sampler"].Value),
		ValueEnumerator.Build()
			.WithName("Cfg")
			.WithHelpText("with cfg of \"8\"")
			.WithDefault("with cfg of \"8\"")
			.WithRegex(new(@"(with cfg of (""|`){1}(?<cfg>.*?)(?:\k<1>))", RegexOptions.Compiled | RegexOptions.IgnoreCase))
			.WithAction((Match e, InputData f) =>
			{
				if (int.TryParse(e.Groups["cfg"].ValueSpan, out var batch))
				{
					f.Cfg = batch;
				}
			}),
		ValueEnumerator.Build()
			.WithName("Seed")
			.WithHelpText("with seed of \"-1\"")
			.WithDefault("with seed of \"-1\"")
			.WithRegex(new(@"(with seed of (""|`){1}(?<seed>.*?)(?:\k<1>))", RegexOptions.Compiled | RegexOptions.IgnoreCase))
			.WithAction((Match e, InputData f) =>
			{
				if (long.TryParse(e.Groups["seed"].ValueSpan, out var batch))
				{
					f.Seed = batch;
				}
			}),
		ValueEnumerator.Build()
			.WithName("face restore")
			.WithHelpText("with face restore \"off\"")
			.WithDefault("with face restore \"off\"")
			.WithRegex(new(@"(with face restore (""|`){1}(?<facerestore>.*?)(?:\k<1>))", RegexOptions.Compiled | RegexOptions.IgnoreCase))
			.WithAction((Match e, InputData f) =>
			{
				if (e.Groups["facerestore"].Value == "on")
				{
					f.FaceRestore = true;
				}
				if (e.Groups["facerestore"].Value == "off")
				{
					f.FaceRestore = false;
				}
			}),
		ValueEnumerator.Build()
			.WithName("Denoise")
			.WithHelpText("denoise of \".3\"")
			.WithDefault("denoise of \".3\"")
			.WithRegex(new(@"(denoise of (""|`){1}(?<cfg>.*?)(?:\k<1>))", RegexOptions.Compiled | RegexOptions.IgnoreCase))
			.WithAction((Match e, InputData f) =>
			{
				if (double.TryParse(e.Groups["cfg"].ValueSpan, CultureInfo.GetCultureInfo("en-us"), out var batch))
				{
					f.DenoiseStrength = batch;
				}
			}),
		ValueEnumerator.Build()
			.WithName("upscale")
			.WithHelpText("upscale ([Optional]with \"latent\") ([Optional]with steps \"2\") ([Optional]sample with \"Sampler\") by \"2\"")
			.WithDefault("upscale with \"none\" with steps \"50\" sample with \"UniPC\" by \"1\"")
			.WithRegex(new(@"upscale( with (""|`){1}(?<upscaler>.*?)(?:\k<2>))?( with steps (""|`){1}(?<steps>.*?)(?:\k<4>))?( sample with (""|`){1}(?<sampler>.*?)(?:\k<6>))? by (""|`){1}(?<scale>.*?)(?:\k<7>)",
				RegexOptions.Compiled | RegexOptions.IgnoreCase))
			.WithAction((Match e, InputData f) =>
			{
				f.Upscaler = e.Groups["upscaler"].Value;
				f.UpscaleSampler = e.Groups["sampler"].Value;
				if (double.TryParse(e.Groups["scale"].ValueSpan, CultureInfo.GetCultureInfo("en-us"), out var scaler))
				{
					f.UpscaleScale = scaler;
				}
				if (int.TryParse(e.Groups["steps"].ValueSpan, CultureInfo.GetCultureInfo("en-us"), out var steps))
				{
					f.UpscalerSteps = steps;
				}
			}),
		ValueEnumerator.Build()
			.WithName("clip skip")
			.WithHelpText("set clip skip to \"2\"")
			.WithDefault("set clip skip to \"1\"")
			.WithRegex(new(@"set clip skip to\\s*(""|`){1}(?<clip_skip>.*?)(?:\k<1>)",
				RegexOptions.Compiled | RegexOptions.IgnoreCase))
			.WithAction((Match e, InputData f) =>
			{
				if (int.TryParse(e.Groups["clip_skip"].ValueSpan, CultureInfo.GetCultureInfo("en-us"), out var scaler))
				{
					f.ClipSkip = scaler;
				}
			}),
	};

	public string Name { get; } = "Generate";

	public string HelpText
	{
		get
		{
			var helpBuilder = new StringBuilder();
			helpBuilder.AppendLine("Help only for Generation")
				.AppendLine("order of instructions after main input does not matter. Duplicates overwrite.")
				.AppendLine(StableDiffusionGeneratorCmd.InputMain.Name)
				.AppendLine(StableDiffusionGeneratorCmd.InputMain.HelpText)
				.AppendLine("<ol>");
			foreach (var valueEnumerator in StableDiffusionGeneratorCmd.Values)
			{
				helpBuilder.AppendLine($"<li>{valueEnumerator.Name}: <code>{valueEnumerator.HelpText}</code></li>");
			}

			helpBuilder.AppendLine("</ol>");
			return helpBuilder.ToString();
		}
	}

	public bool Test(string input, MatrixBotEventArgs botEventArgs)
	{
		return ReadInput(input) != null;
	}

	public static InputData? ReadInput(string text, InputData originalData = null)
	{
		text = text.Trim().Trim('\n');
		var match = InputMain.Regex.Match(text);
		if (match.Success || originalData is not null)
		{
			var data = originalData ?? new InputData(text);
			if (match.Success)
			{
				InputMain.Action(match, data);
			}
			foreach (var valueTuple in Values)
			{
				var valueMatch = valueTuple.Regex.Match(text);
				if (valueMatch.Success)
				{
					valueTuple.Action(valueMatch, data);
				}
				else if (valueTuple.Default is not null && originalData is null)
				{
					valueMatch = valueTuple.Regex.Match(valueTuple.Default);
					if (valueMatch.Success)
					{
						valueTuple.Action(valueMatch, data);
					}
				}
			}

			return data;
		}

		return originalData;
	}

	private void StartWorker()
	{
		Task.Run(async () =>
		{
			while (true)
			{
				await Task.Delay(5000).ConfigureAwait(false);
				while (ProcessQueue.TryDequeue(out var request))
				{
					if (request.Cancelled)
					{
						continue;
					}

					try
					{
						CurrentWork = request;
						await RunGeneration(request.Args, request.InputData).ConfigureAwait(false);
						CurrentWork = default;
					}
					catch (Exception e)
					{
						Console.WriteLine(e);
					}
				}
			}
		});
	}

	private int GetQueueSize()
	{
		if (CurrentWork is null)
		{
			return ProcessQueue.Count;
		}
		return ProcessQueue.Count + 1;
	}

	public int GetQueueImages()
	{
		var currentImages = 0;
		if (CurrentWork is not null)
		{
			currentImages = CurrentWork.InputData.BatchCount * CurrentWork.InputData.BatchSize;
		}

		return ProcessQueue.Sum(e => e.InputData.BatchCount * e.InputData.BatchSize) + currentImages;
	}

	public async Task Run(string input, MatrixBotEventArgs botEventArgs)
	{
		var data = ReadInput(input);
		var stableDiffusionGeneratorCmd = Config._instance.Commands.OfType<RetryCommand>().FirstOrDefault();
		stableDiffusionGeneratorCmd.UserCommands[botEventArgs.Event.Sender] = data;

		await _matrixBot.PostThreadMessage("", $"{Spite("Sure thing", "Alles klar mein liebster")}. " +
		                                       $"You are at #{GetQueueSize() + 1} in {Spite("queue", "WARTESCHLANGE")} " +
		                                       $"with {GetQueueImages()} images to be generated before yours.", botEventArgs).ConfigureAwait(false);

		ProcessQueue.Enqueue(new(botEventArgs.Event.Sender, data.ToString(), DateTime.UtcNow, data, botEventArgs));
	}

	private async Task RunGeneration(MatrixBotEventArgs botEventArgs, InputData? data)
	{
		var lastMessage = await _matrixBot.PostThreadMessage("", $"Heads up {botEventArgs.Event.Sender} i will {Spite("generate", "erzeuge")} your {Spite("image", "bild")} now.", botEventArgs).ConfigureAwait(false);

		var set = Config._instance.ConfigService.ConfigStore.ModelSets[Config._instance.ConfigService.ConfigStore.ActiveSetName];
		if (!set.Models.Contains(data.Model))
		{
			await _matrixBot.UpdateMessage($"Well no, complain about it to my masteru. The requested Model is not part of the selected set of <ol>{string.Join("\r\n", set.Models.Select(e => $"<li><code>{e}</code></li>"))}</ol>", botEventArgs, lastMessage).ConfigureAwait(false);
			return;
		}

		var text2ImageOptions = new StableDiffusionProcessingTxt2Img()
		{
			Prompt = data.Positive,
			Negative_prompt = data.Negative,
			Steps = data.Steps,
			Width = data.SizeX,
			Height = data.SizeY,
			Batch_size = data.BatchSize,
			N_iter = data.BatchCount,
			Send_images = true,
			Cfg_scale = data.Cfg,
			Seed = data.Seed,
			Restore_faces = data.FaceRestore,
			Enable_hr = data.UpscaleScale > 1,
			Denoising_strength = data.DenoiseStrength,
			Hr_second_pass_steps = data.UpscalerSteps,
			Hr_sampler_name = data.UpscaleSampler,
			Hr_scale = data.UpscaleScale,
			Hr_upscaler = data.Upscaler,
			Sampler_name = data.Sampler,
			Do_not_save_grid = true,
			Do_not_save_samples = true,
			AdditionalProperties = new Dictionary<string, object>()
			{
				{ "clip_skip", data.ClipSkip },
				{ "force_hr", data.UpscaleScale > 1},
				{ "hr_force", data.UpscaleScale > 1},
			},
		};

		var stableDiffUrl = Config._instance.StableDiffusionServer;

		var sdApiClient = new V1Client(stableDiffUrl, new HttpClient()
		{
			Timeout = TimeSpan.FromSeconds(Config._instance.ConfigService.ConfigStore.TimeoutInSeconds),
		});
		
		try
		{
			if (!string.IsNullOrWhiteSpace(data.Model))
			{
				var models = await sdApiClient.SdModelsAsync().ConfigureAwait(false);
				var options = await sdApiClient.OptionsGetAsync().ConfigureAwait(false);
				
				var setModelName = Path.GetFileNameWithoutExtension(data.Model);
				var requestedModel = models.FirstOrDefault(e => e.Model_name?.Equals(setModelName) == true);
				if (requestedModel is null)
				{
					await _matrixBot.UpdateMessage($"Sd says it does not know the model '{setModelName}'. Fix your shit {botEventArgs.Event.Sender}",
						botEventArgs, lastMessage).ConfigureAwait(false);
					return;
				}

				var requestedModelPath =
					$"{Path.GetFileName(requestedModel.Filename)} [{requestedModel.Hash}]";

				if (!options.Sd_model_checkpoint.Equals(requestedModelPath))
				{
					options.Sd_model_checkpoint = requestedModelPath;
					var loadModelTask = sdApiClient.OptionsPostAsync(new Options()
					{
						Sd_model_checkpoint = options.Sd_model_checkpoint,
					});

					int overTime = 0;
					var template = Enumerable.Repeat('-', 10).ToArray();

					var typingCounter = Task.Run(async () =>
					{
						while (!loadModelTask.IsCompleted)
						{
							var fromSeconds = TimeSpan.FromSeconds(3);
							await _matrixBot.SendTyping(botEventArgs, fromSeconds + TimeSpan.FromSeconds(1), true).ConfigureAwait(false);

							var progressMessage = template.ToArray();
							progressMessage[overTime++ % 10] = 'o';
							await _matrixBot.UpdateMessage($"Loading model into vram [{string.Join("", progressMessage)}]", botEventArgs,
								lastMessage).ConfigureAwait(false);
							await Task.Delay(3000).ConfigureAwait(false);
						}
					});
					var result = await loadModelTask.ConfigureAwait(false);
					await typingCounter.ConfigureAwait(false);
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"No Stable Diffusion detected at {stableDiffUrl}. Run webui-user.bat with:\n" +
							  "set COMMANDLINE_ARGS=--api\n" +
							  "in the webui-user.bat file for Automatic1111 Stable Diffusion.\n" +
							  "Stable Diffusion error message: " + ex);

			await _matrixBot.UpdateMessage("Seems like there was an issue with your request. It failed. sooooooorryy. NEXT!",
				botEventArgs, lastMessage).ConfigureAwait(false);
			return;
		}

		var textToImageResponse = sdApiClient.Txt2imgAsync(text2ImageOptions);

		var task = Task.Run(async () =>
		{
			Modules__api__models__ProgressResponse progress = new Modules__api__models__ProgressResponse()
			{
				Progress = 0,
				Eta_relative = 0
			};
			var updateMessage = new StringBuilder();

			while (!textToImageResponse.IsCompleted)
			{
				var fromSeconds = TimeSpan.FromSeconds(3);
				await _matrixBot.SendTyping(botEventArgs, fromSeconds + TimeSpan.FromSeconds(1), true).ConfigureAwait(false);
				var innerProgress = await sdApiClient.ProgressAsync(true).ConfigureAwait(false);
				if (innerProgress.Progress != 0)
				{
					progress = innerProgress;
				}

				updateMessage.Clear();
				updateMessage.AppendLine($"Progress: {progress.Progress * 100}")
					.AppendLine($"Eta: {TimeSpan.FromSeconds(progress.Eta_relative)}");
				await _matrixBot.UpdateMessage(updateMessage.ToString(), botEventArgs, lastMessage).ConfigureAwait(false);

				await Task.Delay(3000).ConfigureAwait(false);
			}

			updateMessage.Clear();
			updateMessage
				.AppendLine($"Progress: 100")
				.AppendLine($"Eta: {TimeSpan.FromSeconds(0)}");
			await _matrixBot.UpdateMessage(updateMessage.ToString(), botEventArgs, lastMessage).ConfigureAwait(false);

		});

		try
		{
			var sw = new Stopwatch();
			sw.Start();
			
			var sdImgResponse = await textToImageResponse.ConfigureAwait(false);
			sw.Stop();
			
			var imageUrls = new List<string>();
			var panicCommand = Config._instance.Commands.OfType<PanicCommand>().First();
			foreach (var imageBase64 in sdImgResponse.Images)
			{
				//string base64 = imageBase64.ToString().Split(",", 2)[1];
				string imageData = imageBase64.ToString();
				int commaIndex = imageData.IndexOf(',') + 1;
				string base64 = imageData.Substring(commaIndex);


				// Decode the base64 string to an image
				using var imageStream = new MemoryStream(Convert.FromBase64String(base64));
				using var image = await Image.LoadAsync(imageStream).ConfigureAwait(false);
				var imageStreamLength = imageStream.Length;
				// Save the image
				var filename = $"{Guid.NewGuid():N}.png";
				imageUrls.Add(filename);
				MatrixBotJsonFileUploadContent? matrixBotJsonFileUploadContent = null;
				await Policy.Handle<Exception>()
					.RetryAsync()
					.ExecuteAndCaptureAsync(async () =>
					{
						imageStream.Seek(0, SeekOrigin.Begin);
						matrixBotJsonFileUploadContent =
							await _matrixBot.UploadFile(imageStream, filename, "image/png").ConfigureAwait(false);
					}).ConfigureAwait(false);

				var imageSend = await _matrixBot.PostRoomImage(filename, matrixBotJsonFileUploadContent, image.Size, imageStreamLength, botEventArgs, data.InPublic).ConfigureAwait(false);
				panicCommand.AddImage(botEventArgs, imageSend);
			}

			var imgMsg =
				$"Hello {botEventArgs.Event.Sender} here {Spite("is", "ist")} are {Spite("image", "bIlD")}/s seeds: \n<ol>" +
				string.Join("\r\n", JObject.Parse(sdImgResponse.Info).GetValue("all_seeds")
					.Values<long>()
					.Select(e => $"<li><code>{e}</code></li>")) +
				$"\n</ol> for the {Spite("prompt", "AnFrAGe")}: {data} \n" +
				$"and it took {sw.Elapsed:g} to render all that";

			await task.ConfigureAwait(false);
			await _matrixBot.PostThreadMessage("", imgMsg, botEventArgs).ConfigureAwait(false);
		}
		catch (ApiException exception)
		{
			await _matrixBot.PostThreadMessage("",
				"Seems like there was an issue with your request. It failed. sooooooorryy. NEXT!",
				botEventArgs).ConfigureAwait(false);
		}
	}
}