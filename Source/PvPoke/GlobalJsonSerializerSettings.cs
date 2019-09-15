using Newtonsoft.Json;
using PvPoke.JsonSerialization;

namespace PvPoke
{
	public static class GlobalJsonSerializerSettings
	{
		public static  JsonSerializerSettings Shared => new JsonSerializerSettings
		{
			ContractResolver = new OrderedPropertiesContractResolver(),
			Converters = { new OrderedExpandoPropertiesConverter() },
			Formatting = Formatting.Indented,
			NullValueHandling = NullValueHandling.Ignore
		};
	}
}