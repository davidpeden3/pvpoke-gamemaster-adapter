using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PvPoke.JsonSerialization
{
	public class OrderedExpandoPropertiesConverter : ExpandoObjectConverter
	{
		public override bool CanWrite => true;

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var expando = (IDictionary<string, object>)value;
			var orderedDictionary = expando.OrderBy(x => x.Key).ToDictionary(t => t.Key, t => t.Value);
			serializer.Serialize(writer, orderedDictionary);
		}
	}
}