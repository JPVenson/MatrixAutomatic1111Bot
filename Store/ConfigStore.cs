using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkribeSeinSDBot.Store
{
	public class ConfigStore
	{
		public IList<string> AdminUsers { get; set; } = new List<string>();
		public IDictionary<string, string> Defaults { get; set; } = new Dictionary<string, string>();
		public IList<string> LockedSettings { get; set; } = new List<string>();
		public IDictionary<string, ModelSet> ModelSets { get; set; } = new Dictionary<string, ModelSet>()
		{
			{"default", new ModelSet()
			{
				Models = new List<string>()
				{
					"crystalClearXL_ccxl"
				}
			}}
		};

		public string ActiveSetName { get; set; } = "default";
		public string BotName { get; set; }

		public string[] RoomsEnabled { get; set; } = new[]
		{
			//"!yWgaSvPuzuHyYeRbOz:bitwrk.de",
			//"!RAHXwBiecbIooZzvvD:bitwrk.de",
			"!xvrTUstJWtVexDyyru:bitwrk.de"
		};

		public int TimeoutInSeconds { get; set; } = (int)TimeSpan.FromMinutes(30).TotalSeconds;
	}

	public class ModelSet
	{
		public IList<string> Models { get; set; } = new List<string>();
	}
}
