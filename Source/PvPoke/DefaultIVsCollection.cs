using System.Collections.Generic;

namespace PvPoke
{
	public class DefaultIVsCollection
	{
		public Dictionary<string, Dictionary<string, List<decimal>>> Pokemon { get; set; } = new Dictionary<string, Dictionary<string, List<decimal>>>();
	}
}