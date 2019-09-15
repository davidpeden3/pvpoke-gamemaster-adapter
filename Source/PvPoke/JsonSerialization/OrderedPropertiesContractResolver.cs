using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PvPoke.JsonSerialization
{
	public class OrderedPropertiesContractResolver : CamelCasePropertyNamesContractResolver
	{
		protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
		{
			return base.CreateProperties(type, memberSerialization).OrderBy(p => p.PropertyName).ToList();
		}
	}
}