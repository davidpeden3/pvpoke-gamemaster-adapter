using System.Collections.Generic;

namespace PvPoke
{
	public class LegacyMoveCollection
	{
		public List<PokemonWithLegacyMoves> Pokemon { get; set; } = new List<PokemonWithLegacyMoves>();

		public class PokemonWithLegacyMoves
		{
			public string SpeciesId { get; set; }
			public List<string> LegacyFastMoves { get; set; } = new List<string>();
			public List<string> LegacyChargeMoves { get; set; } = new List<string>();
		}
	}
}