using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SkribeSeinSDBot.SdApiClient
{
	public partial class V1Client
	{
		partial void UpdateJsonSerializerSettings(JsonSerializerSettings settings)
		{
			settings.ContractResolver = new SafeContractResolver();
			settings.DefaultValueHandling = DefaultValueHandling.Ignore;
		}

		class SafeContractResolver : DefaultContractResolver
		{
			protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
			{
				var jsonProp = base.CreateProperty(member, memberSerialization);
				jsonProp.Required = Required.Default;
				return jsonProp;
			}
		}
	}
}
