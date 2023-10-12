using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SkribeSeinSDBot.Commands;

namespace SkribeSeinSDBot.Store
{
	public class ConfigStoreService
	{
		public ConfigStore ConfigStore { get; private set; }

		public ConfigStoreService()
		{
			
		}

		public async ValueTask Save()
		{
			var serializeObject = JsonConvert.SerializeObject(ConfigStore, Formatting.Indented);
			await File.WriteAllTextAsync("./config.json", serializeObject).ConfigureAwait(false);
		}

		public async ValueTask Load()
		{
			if (File.Exists("./config.json"))
			{
				ConfigStore = JsonConvert.DeserializeObject<ConfigStore>(await File.ReadAllTextAsync("./config.json").ConfigureAwait(false))!;
			}
			else
			{
				ConfigStore = new ConfigStore();
			}

			foreach (var configStoreDefault in ConfigStore.Defaults)
			{
				var firstOrDefault = StableDiffusionGeneratorCmd.Values.FirstOrDefault(e => e.Name.Equals(configStoreDefault.Key, StringComparison.InvariantCultureIgnoreCase));
				firstOrDefault.Default = configStoreDefault.Value;
			}
			await Save().ConfigureAwait(false);
		}
	}
}
