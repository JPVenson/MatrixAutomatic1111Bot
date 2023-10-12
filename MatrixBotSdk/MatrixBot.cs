using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Retry;

namespace SkribeSeinSDBot.MatrixBotSdk;

public interface ILogger
{
	public void Info(string message);
	public void Error(string message);
}

class SimpleConsoleLogger : ILogger
{
	void ILogger.Error(string message)
	{
		Console.WriteLine($"ERROR {message}");
	}

	void ILogger.Info(string message)
	{
		Console.WriteLine($"INFO {message}");
	}
}

public class MatrixBot
{
	private readonly string _namePrefix;
	public event EventHandler<MatrixBotEventArgs>? OnEvent;
	private event EventHandler? OnSync;
	public int SyncTimeout { get; set; } = 10000;

	private readonly IMatrixBotStore _storage;
	private readonly HttpClient HttpClient;

	private Uri? _serverUri;
	private ILogger _logger;
	private MatrixBotConfig? _config;

	private long _requestId = 0L;
	private bool _isSyncing = false;

	private BlockingCollection<Func<Task>> _actionQueue = new BlockingCollection<Func<Task>>();

	public bool IsSyncing
	{
		get { return _isSyncing; }
	}

	public MatrixBot(ILogger? logger = null, IMatrixBotStore? storage = null, string namePrefix = null)
	{
		_namePrefix = namePrefix;

		if (logger == null)
		{
			_logger = new SimpleConsoleLogger();
		}
		else
		{
			_logger = logger;
		}


		if (storage == null)
		{
			_storage = new MatrixBotSimpleFileStorage();
		}
		else
		{
			_storage = storage;
		}


		HttpClient = new HttpClient();
		_actionQueue = new BlockingCollection<Func<Task>>();

		Task.Factory.StartNew(async () =>
		{
			foreach (var func in _actionQueue.GetConsumingEnumerable())
			{
				try
				{
					await func().ConfigureAwait(false);
				}
				catch (Exception e)
				{
					Console.WriteLine();
				}
			}
		});
	}

	public async void Start()
	{
		_config = _storage.Read();

		if (_config is null)
		{
			_config = new MatrixBotConfig();
			_logger.Info("configuration was empty - created default storage");
			_storage.Write(_config);
		}

		_serverUri = new Uri(_config.ServerUri);

		if (string.IsNullOrEmpty(_config.AccessToken))
		{
			try
			{
				var login = await DoRequestClient<MatrixBotJsonLogin>("login", HttpMethod.Post,
					new { type = "m.login.password", user = _config.Username, password = _config.Password }).ConfigureAwait(false);

				_config.AccessToken = login?.AccessToken;
				_config.UserId = login?.UserId;

				_storage.Write(_config);
			}
			catch (HttpRequestException)
			{
				_logger.Error("no access token available and credentials invalid.");
				return;
			}
		}
		
		_isSyncing = true;
		Task.Factory.StartNew(async () =>
		{
			while (_isSyncing)
			{
				try
				{
					await Sync(_config?.Since).ConfigureAwait(false);

					_logger.Info($"synced since {_config?.Since}");

					var handler = OnSync;
					if (handler is not null)
					{
						handler(this, new EventArgs());
					}
				}
				catch (Exception e)
				{
					Console.WriteLine("Error while syncing");
				}
			}
		}, TaskCreationOptions.LongRunning);
	}

	public void Stop()
	{
		_isSyncing = false;
	}

	private async Task Sync(string? since)
	{
		var sync = await DoRequestClient<MatrixBotJsonSync>($"sync", HttpMethod.Get, null,
			new string[] { "full_state=false", $"timeout={SyncTimeout}", since == null ? "_=0" : $"since={since}" }).ConfigureAwait(false);

		if (sync is null)
		{
			_logger.Error("sync returned null ?");
			return;
		}

		if (_config is null)
		{
			_logger.Error("_config is null ?");
			return;
		}

		_config.Since = sync.NextBatch;

		_storage.Write(_config);

		if (sync.Rooms is not null && sync.Rooms.Join is not null)
		{
			foreach (var room in sync.Rooms.Join)
			{
				var roomId = room.Key;
				if (room.Value.TimeLine is not null && room.Value.TimeLine.Events is not null)
				{
					foreach (var ev in room.Value.TimeLine.Events)
					{
						if (ev.Sender != _config.UserId)
						{
							var args = new MatrixBotEventArgs(roomId, ev);
							var handler = OnEvent;
							if (handler is not null)
							{
								handler(this, args);
							}
						}
					}
				}
			}
		}
	}

	public async Task<MatrixBotJsonSend?> PostRoomMessage(string roomId, string plain, string formatted)
	{
		var content = new
		{
			msgtype = "m.notice",
			format = "org.matrix.custom.html",
			body = _namePrefix + plain,
			formatted_body = _namePrefix + formatted
		};
		return await DoRequestClient<MatrixBotJsonSend>($"rooms/{Uri.EscapeDataString(roomId)}/send/m.room.message",
			HttpMethod.Post, content).ConfigureAwait(false);
	}

	public async Task<MatrixBotJsonSend?> PostRoomMessage(string roomId, string plain, string formatted,
		string inReplyTo)
	{
		var content = new JObject()
		{
			{ "msgtype", "m.notice" },
			{ "body", _namePrefix + plain },
			{ "format", "org.matrix.custom.html" },
			{ "formatted_body", _namePrefix + formatted },
			{
				"m.relates_to", new JObject()
				{
					{
						"m.in_reply_to", new JObject()
						{
							{ "event_id", inReplyTo }
						}
					},
					{ "is_falling_back", true },
					{ "rel_type", "m.thread" }
				}
			}
		};
		return await DoRequestClient<MatrixBotJsonSend>($"rooms/{Uri.EscapeDataString(roomId)}/send/m.room.message",
			HttpMethod.Post, content).ConfigureAwait(false);
	}

	public async Task<MatrixBotJsonSend?> PostThreadMessage(string plain, string formatted, MatrixBotEventArgs caller)
	{
		var content = new JObject()
		{
			{ "msgtype", "m.notice" },
			{ "body", _namePrefix + plain },
			{ "format", "org.matrix.custom.html" },
			{ "formatted_body", _namePrefix + formatted },
			{
				"m.relates_to", new JObject()
				{
					{
						"event_id",
						caller.Event.Content?.Relates?.InReplyTo?.EventId == null ? caller.Event.EventId : null
					},
					{
						"m.in_reply_to", new JObject()
						{
							{ "event_id", caller.Event.Content?.Relates?.InReplyTo?.EventId ?? caller.Event.EventId }
						}
					},
					{ "is_falling_back", true },
					{ "rel_type", "m.thread" }
				}
			}
		};
		return await DoRequestClient<MatrixBotJsonSend>(
			$"rooms/{Uri.EscapeDataString(caller.RoomId)}/send/m.room.message", HttpMethod.Post, content).ConfigureAwait(false);
	}

	public async Task<MatrixBotJsonSend?> UpdateMessage(string formatted, MatrixBotEventArgs caller, MatrixBotJsonSend originalMessage)
	{
		var content = new JObject()
		{
			{ "msgtype", "m.notice" },
			{ "body", _namePrefix + formatted },
			{ "format", "org.matrix.custom.html" },
			{ "formatted_body", _namePrefix + formatted },
			{
				"m.new_content", new JObject()
				{
					{ "msgtype", "m.notice" },
					{ "body", _namePrefix + formatted },
					{ "format", "org.matrix.custom.html" },
					{ "formatted_body", _namePrefix + formatted },
				}
			},
			{
				"m.relates_to", new JObject()
				{
					{ "rel_type", "m.replace" },
					{ "event_id", originalMessage.EventId },
					{
						"m.in_reply_to", new JObject()
						{
							{ "event_id", caller.Event.Content?.Relates?.InReplyTo?.EventId ?? caller.Event.EventId }
						}
					},
					{ "is_falling_back", true },
				}
			}
		};
		return await DoRequestClient<MatrixBotJsonSend>(
			$"rooms/{Uri.EscapeDataString(caller.RoomId)}/send/m.room.message", HttpMethod.Post, content).ConfigureAwait(false);
	}

	public async Task<MatrixBotJsonSend?> PostRoomImage(string plain, MatrixBotJsonFileUploadContent matrixDataElement,
		Size imageSize,
		long imageStreamLength,
		MatrixBotEventArgs caller,
		bool dataInPublic)
	{
		var content = new JObject()
		{
			{ "msgtype", "m.image" },
			{ "body", plain },
			{ "url", matrixDataElement.ContentUri },
			{
				"info", new JObject()
				{
					{ "size", imageStreamLength },
					{ "h", imageSize.Height },
					{ "w", imageSize.Width },
					{ "mimetype", "image/png" },
				}
			},
			{
				"m.relates_to", new JObject()
				{
					{
						"event_id", dataInPublic ? null : (caller.Event.Content?.Relates?.InReplyTo?.EventId == null ? caller.Event.EventId : null)
					},
					{
						"m.in_reply_to", new JObject()
						{
							{ "event_id", (dataInPublic ? null : caller.Event.Content?.Relates?.InReplyTo?.EventId) ?? caller.Event.EventId }
						}
					},
					{ "is_falling_back", true },
					{ "rel_type", "m.thread" }
				}
			}
		};
		return await DoRequestClient<MatrixBotJsonSend>(
			$"rooms/{Uri.EscapeDataString(caller.RoomId)}/send/m.room.message", HttpMethod.Post, content).ConfigureAwait(false);
	}

	public async Task<MatrixBotJsonRooms?> GetJoinedRooms()
	{
		return await DoRequestClient<MatrixBotJsonRooms>($"joined_rooms", HttpMethod.Get).ConfigureAwait(false);
	}

	public async Task<MatrixBotJsonProfile?> GetProfile(string? senderId = null)
	{
		return await DoRequestClient<MatrixBotJsonProfile>($"profile/{senderId ?? _config?.UserId}", HttpMethod.Get).ConfigureAwait(false);
	}

	public async Task<MatrixBotJsonJoin?> PostJoinRoom(string roomId)
	{
		return await DoRequestClient<MatrixBotJsonJoin>($"join/{Uri.EscapeDataString(roomId)}", HttpMethod.Post).ConfigureAwait(false);
	}

	public async Task<MatrixBotJsonStateContent?> StateEvent(string roomId, string eventType, string stateKey)
	{
		return await DoRequestClient<MatrixBotJsonStateContent>(
			$"rooms/{Uri.EscapeDataString(roomId)}/state/{eventType}/{stateKey}", HttpMethod.Get).ConfigureAwait(false);
	}

	public async Task<MatrixBotJsonFileUploadContent?> UploadFile(Stream mediaStream, string filename,
		string contentType)
	{
		return await DoRequestMedia<MatrixBotJsonFileUploadContent>($"upload/", HttpMethod.Post, mediaStream, new[]
		{
			$"filename={Uri.EscapeDataString(filename)}",
			"onlyContentUri=false"
		}, new[]
		{
			("Content-Type", contentType)
		}).ConfigureAwait(false);
	}

	private async Task<T?> DoRequestClient<T>(string path, HttpMethod method, object? body = null,
		string[]? query = null, (string name, string value)[]? header = null)
	{
		var q = $"?access_token={_config?.AccessToken}";
		if (query != null)
		{
			q = $"{q}&{string.Join("&", query)}";
		}
		
		return await DoRequestSend<T>(body, header, () => new HttpRequestMessage(method, $"{_serverUri}_matrix/client/r0/{path}{q}")).ConfigureAwait(false);
	}

	private async Task<T?> DoRequestMedia<T>(string path, HttpMethod method, object? body = null,
		string[]? query = null, (string name, string value)[]? header = null)
	{
		var q = $"?access_token={_config?.AccessToken}";
		if (query != null)
		{
			q = $"{q}&{string.Join("&", query)}";
		}
		
		return await DoRequestSend<T>(body, header, () => new HttpRequestMessage(method, $"{_serverUri}_matrix/media/v3/{path}{q}")).ConfigureAwait(false);
	}
	private async Task<T?> DoRequestSend<T>(object? body, (string name, string value)[]? header,
		Func<HttpRequestMessage> request)
	{
		using (var response = await Send(body, header, request).ConfigureAwait(false))
		{
			var jsonString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			return JsonSerializer.Deserialize<T>(jsonString);
		}
	}

	public async Task SendTyping(MatrixBotEventArgs botEventArgs, TimeSpan fromSeconds, bool value)
	{
		var query =
			$"{_serverUri}_matrix/client/r0/rooms/{Uri.EscapeDataString(botEventArgs.RoomId)}/typing/{Uri.EscapeDataString(_config.UserId)}?access_token={_config?.AccessToken}";
		await Send(new SendTypingMessage()
			{
				Timeout = (int)fromSeconds.TotalMilliseconds,
				Typing = value
			},
			null,
			() => new HttpRequestMessage(HttpMethod.Put, query)).ConfigureAwait(false);
	}

	private async Task<HttpResponseMessage> Send(object? body, (string name, string value)[]? header,
		Func<HttpRequestMessage> requestFac)
	{
		HttpRequestMessage BuildRequest(HttpRequestMessage request)
		{
			if (body != null)
			{
				if (body is Stream streamBody)
				{
					request.Content = new StreamContent(streamBody);
				}
				else if (body is not JObject)
				{
					request.Content = new StringContent(JsonSerializer.Serialize(body));
				}
				else if (body is JObject jBody)
				{
					request.Content = new StringContent(jBody.ToString());
				}
			}

			if (header != null)
			{
				foreach (var s in header)
				{
					request.Headers.TryAddWithoutValidation(s.name, s.value);
				}
			}

			return request;
		}

		HttpResponseMessage response = null;
		try
		{
			var doneTask = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
			int retryCounter = 1;

			async Task SendInner(HttpRequestMessage request)
			{
				var httpResponseMessage = await HttpClient.SendAsync(request).ConfigureAwait(false);
				httpResponseMessage?.EnsureSuccessStatusCode();
				doneTask.SetResult(httpResponseMessage);
			}

			void TrySend()
			{
				if (retryCounter++ == 5)
				{
					doneTask.SetException(new Exception("Failed retry"));
					return;
				}
				_actionQueue.Add(async () =>
				{
					try
					{
						using var httpRequestMessage = BuildRequest(requestFac());
						await SendInner(httpRequestMessage).ConfigureAwait(false);
					}
					catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.TooManyRequests)
					{
						Task.Delay(5000)
							.ContinueWith(task => TrySend());
					}
					catch(Exception e) 
					{
						doneTask.SetException(e);
					}
				});
			}

			_actionQueue.Add(async () =>
			{
				try
				{
					using var httpRequestMessage = BuildRequest(requestFac());
					await SendInner(httpRequestMessage).ConfigureAwait(false);
				}
				catch
				{
					TrySend();
				}
			});

			response = await doneTask.Task.ConfigureAwait(false);
		}
		catch (HttpRequestException e)
		{
			if (response != null)
			{
				Console.WriteLine("Error: " + await response.Content.ReadAsStringAsync().ConfigureAwait(false));
				response?.Dispose();
			}
			throw;
		}

		return response;
	}
}

public class SendTypingMessage
{
	[JsonPropertyName("typing")]
	public bool Typing { get; set; }
	[JsonPropertyName("timeout")]
	public int Timeout { get; set; }
}

public class MatrixBotEventArgs : EventArgs
{
	public string RoomId { get; }
	public MatrixBotJsonSyncEvent Event { get; }

	public MatrixBotEventArgs(string roomId, MatrixBotJsonSyncEvent ev)
	{
		RoomId = roomId;
		Event = ev;
	}
}

public class MatrixBotJsonSend
{
	[JsonPropertyName("event_id")] public string? EventId { get; set; }
}

public class MatrixBotJsonJoin
{
	[JsonPropertyName("room_id")] public string? RoomId { get; set; }
}

public class MatrixBotJsonLogin
{
	[JsonPropertyName("access_token")] public string? AccessToken { get; set; }
	[JsonPropertyName("home_server")] public string? HomeServer { get; set; }
	[JsonPropertyName("user_id")] public string? UserId { get; set; }
}

public class MatrixBotJsonProfile
{
	[JsonPropertyName("avatar_url")] public string? AvatarUrl { get; set; }
	[JsonPropertyName("displayname")] public string? DisplayName { get; set; }
}

public class MatrixBotJsonRooms
{
	[JsonPropertyName("joined_rooms")] public string[]? JoinedRooms { get; set; }
}

public class MatrixBotJsonSync
{
	[JsonPropertyName("rooms")] public MatrixBotJsonSyncRooms? Rooms { get; set; }
	[JsonPropertyName("next_batch")] public string? NextBatch { get; set; }
}

public class MatrixBotJsonSyncRooms
{
	[JsonPropertyName("join")] public Dictionary<string, MatrixBotJsonSyncTimeLine>? Join { get; set; }
}

public class MatrixBotJsonSyncTimeLine
{
	[JsonPropertyName("timeline")] public MatrixBotJsonSyncEvents? TimeLine { get; set; }
}

public class MatrixBotJsonSyncEvents
{
	[JsonPropertyName("events")] public MatrixBotJsonSyncEvent[]? Events { get; set; }
}

public class MatrixBotJsonSyncEvent
{
	[JsonPropertyName("content")] public MatrixBotJsonSyncEventContent? Content { get; set; }
	[JsonPropertyName("type")] public string? Type { get; set; }
	[JsonPropertyName("event_id")] public string? EventId { get; set; }
	[JsonPropertyName("sender")] public string? Sender { get; set; }
}

public class MatrixBotJsonSyncEventContent
{
	[JsonPropertyName("body")] public string? Body { get; set; }
	[JsonPropertyName("msgtype")] public string? MsgType { get; set; }
	[JsonPropertyName("format")] public string? Format { get; set; }
	[JsonPropertyName("formatted_body")] public string? FormattedBody { get; set; }
	[JsonPropertyName("membership")] public string? Membership { get; set; }
	[JsonPropertyName("displayname")] public string? DisplayName { get; set; }
	[JsonPropertyName("avatar_url")] public string? AvatarUrl { get; set; }

	[JsonPropertyName("m.relates_to")] public MatrixBotJsonSyncEventContentRelation? Relates { get; set; }
}

public class MatrixBotJsonSyncEventContentRelation
{
	[JsonPropertyName("m.in_reply_to")] public MatrixBotJsonSyncEventContentRelationReply? InReplyTo { get; set; }
}

public class MatrixBotJsonSyncEventContentRelationReply
{
	[JsonPropertyName("event_id")] public string EventId { get; set; }
}

public class MatrixBotJsonStateContent
{
	[JsonPropertyName("aliases")] //m.room.aliases
	public string[]? Aliases { get; set; }

	[JsonPropertyName("creator")] //m.room.create
	public string? Creator { get; set; }

	[JsonPropertyName("join_rule")] // m.room.join_rules
	public string? JoinRule { get; set; }

	[JsonPropertyName("name")] // m.room.name
	public string? Name { get; set; }
}

public class MatrixBotJsonFileUploadContent
{
	[JsonPropertyName("content_uri")] // m.room.name
	public string? ContentUri { get; set; }
}

public interface IMatrixBotStore
{
	MatrixBotConfig? Read();
	void Write(MatrixBotConfig config);
}

public class MatrixBotConfig
{
	public string? Since { get; set; }
	public string? AccessToken { get; set; }
	public string? UserId { get; set; }
	public string ServerUri { get; set; } = "https://matrix.org";
	public string Username { get; set; } = "john.doe";
	public string Password { get; set; } = "s3cr3t";
}

class MatrixBotSimpleFileStorage : IMatrixBotStore
{
	MatrixBotConfig? IMatrixBotStore.Read()
	{
		try
		{
			var json = File.ReadAllText(@"matrixbot.json");
			return JsonSerializer.Deserialize<MatrixBotConfig>(json);
		}
		catch (FileNotFoundException)
		{
			return null;
		}
	}

	void IMatrixBotStore.Write(MatrixBotConfig config)
	{
		File.WriteAllText(@"matrixbot.json", JsonSerializer.Serialize(config));
	}
}