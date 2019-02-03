using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PvPoke.FileManagement;
using PvPoke.FileManagement.PokemonGo;
using PvPoke.FileManagement.PvPoke;
using Xunit;
using Xunit.Abstractions;

namespace PvPoke.UnitTest
{
	public class FileManagerTests
	{
		private static readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
		{
			ContractResolver = new CamelCasePropertyNamesContractResolver(),
			Formatting = Formatting.Indented,
			NullValueHandling = NullValueHandling.Ignore
		};

		private readonly ITestOutputHelper _output;

		public FileManagerTests(ITestOutputHelper output)
		{
			_output = output;
		}

		[Fact]
		public async Task RoundTripPvPokeJson()
		{
			var json = await PvPokeGameMasterFileManager.ReadFileAsync();
			PvPokeGameMasterFileManager.GameMasterFile file = PvPokeGameMasterFileManager.LoadFile(json);

			foreach (var pokemonProperty in file.Pokemon)
			{
				pokemonProperty.FastMoves = pokemonProperty.FastMoves.Select(m => m).OrderBy(m => m).ToList();
				pokemonProperty.ChargedMoves = pokemonProperty.ChargedMoves.Select(m => m).OrderBy(m => m).ToList();
				if (pokemonProperty.LegacyMoves != null)
				{
					pokemonProperty.LegacyMoves = pokemonProperty.LegacyMoves.Select(m => m).OrderBy(m => m).ToList();
				}
			}

			file.Moves = file.Moves.Select(m => m).OrderBy(m => m.Name);

			string serializedFile = file.ToJson();
			_output.WriteLine(serializedFile);
			//Assert.Equal(json, serializedFile);
		}

		//[Fact(Skip = "Assert that we can round trip the raw json")]
		[Fact]
		public async Task GeneratePvpokeGameMaster()
		{
			//await PokemonGoGameMasterFileManager.FetchAndSaveFileAsync();
			var json = await PokemonGoGameMasterFileManager.ReadFileAsync();
			dynamic gameMaster = JsonConvert.DeserializeObject<dynamic>(json);

			var optionsJson = await FileManager.ReadFileAsync(Path.Combine(AppContext.BaseDirectory, "Data", "options.json"));
			var options = FileManager.LoadFile<dynamic>(optionsJson);

			var cupsJson = await FileManager.ReadFileAsync(Path.Combine(AppContext.BaseDirectory, "Data", "cups.json"));
			var cups = FileManager.LoadFile<dynamic>(cupsJson);

			var gameMasterFile = new PvPokeGameMasterFileManager.GameMasterFile
			{
				Options = options,
				Cups = cups,
				Pokemon = await GameMasterFileAdapter.AdaptPokemonAsync(gameMaster),
				Moves = GameMasterFileAdapter.AdaptMoves(gameMaster)
			};

			_output.WriteLine(JsonConvert.SerializeObject(gameMasterFile, _jsonSerializerSettings));
		}

		[Fact]
		public async Task CreateLegacyMovesConfiguration()
		{
			var json = await PokemonGoGameMasterFileManager.ReadFileAsync();
			dynamic gameMaster = JsonConvert.DeserializeObject<dynamic>(json);
			var regex = new Regex(@"^COMBAT_V\d+_MOVE_");
			var templates = ((IEnumerable<dynamic>)gameMaster.itemTemplates).Where(t => regex.IsMatch((string)t.templateId));

			var moves = new Dictionary<string, bool>();

			foreach (dynamic template in templates)
			{
				string moveId = (string)template.combatMove.uniqueId;
				moves.Add(moveId.Replace("_FAST", String.Empty), moveId.EndsWith("_FAST"));
			}

			var pvpokeJson = await PvPokeGameMasterFileManager.ReadFileAsync();
			var pvpokeGameMaster = JsonConvert.DeserializeObject<PvPokeGameMasterFileManager.GameMasterFile>(pvpokeJson);
			var legacyMoveCollection = new LegacyMoveCollection();

			foreach (var pokemon in pvpokeGameMaster.Pokemon)
			{
				var pokemonWithLegacyMoves = new LegacyMoveCollection.PokemonWithLegacyMoves {SpeciesId = pokemon.SpeciesId};

				if (pokemon.LegacyMoves != null)
				{
					foreach (string legacyMove in pokemon.LegacyMoves)
					{
						if (legacyMove.StartsWith("HIDDEN_POWER_") || moves[legacyMove])
						{
							pokemonWithLegacyMoves.LegacyFastMoves.Add(legacyMove);
						}
						else
						{
							pokemonWithLegacyMoves.LegacyChargeMoves.Add(legacyMove);
						}
					}
				}

				legacyMoveCollection.Pokemon.Add(pokemonWithLegacyMoves);
			}

			_output.WriteLine(JsonConvert.SerializeObject(legacyMoveCollection, _jsonSerializerSettings));
		}
	}

	public static class GameMasterFileAdapter
	{
		private static readonly IEnumerable<string> _hiddenPowers = new List<string>
		{
			"HIDDEN_POWER_BUG",
			"HIDDEN_POWER_DARK",
			"HIDDEN_POWER_DRAGON",
			"HIDDEN_POWER_ELECTRIC",
			"HIDDEN_POWER_FIGHTING",
			"HIDDEN_POWER_FIRE",
			"HIDDEN_POWER_FLYING",
			"HIDDEN_POWER_GHOST",
			"HIDDEN_POWER_GRASS",
			"HIDDEN_POWER_GROUND",
			"HIDDEN_POWER_ICE",
			"HIDDEN_POWER_POISON",
			"HIDDEN_POWER_PSYCHIC",
			"HIDDEN_POWER_ROCK",
			"HIDDEN_POWER_STEEL",
			"HIDDEN_POWER_WATER"
		};

		public static async Task<IEnumerable<PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty>> AdaptPokemonAsync(dynamic gameMaster)
		{
			var regex = new Regex(@"^V\d+_POKEMON_");
			//var regex = new Regex(@"^V0006_POKEMON_CHARIZARD$");
			var templates = ((IEnumerable<dynamic>)gameMaster.itemTemplates).Select(t => t).Where(t => regex.IsMatch((string)t.templateId));

			var pokemon = new Dictionary<string, PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty>();

			foreach (dynamic template in templates)
			{
				string speciesId = GenerateSpeciesId(template);

				var pokemonProperty = new PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty
				{
					Dex = Convert.ToInt32(((string)template.templateId).Substring(1, 4)),
					SpeciesName = GenerateSpeciesName(speciesId),
					SpeciesId = speciesId,
					BaseStats = new PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty.BaseStatsProperty
					{
						Atk = template.pokemonSettings.stats.baseAttack,
						Def = template.pokemonSettings.stats.baseDefense,
						Hp = template.pokemonSettings.stats.baseStamina,
					},
					Types = new List<string>
					{
						FormatType((string)template.pokemonSettings.type),
						FormatType((string)template.pokemonSettings.type2)
					},
					FastMoves = new List<string>(((IEnumerable<string>)template.pokemonSettings.quickMoves.ToObject<IEnumerable<string>>()).Select(GenerateMoveId).Distinct().OrderBy(m => m)),
					ChargedMoves = new List<string>(((IEnumerable<string>)template.pokemonSettings.cinematicMoves.ToObject<IEnumerable<string>>()).Select(GenerateMoveId).Distinct().OrderBy(m => m)),
					LegacyMoves = new List<string>()
				};

				pokemon.Add(speciesId, pokemonProperty);
			}

			var legacyMovesJson = await FileManager.ReadFileAsync(Path.Combine(AppContext.BaseDirectory, "Data", "legacyMoves.json"));
			var legacyMoves = FileManager.LoadFile<LegacyMoveCollection>(legacyMovesJson);

			foreach (LegacyMoveCollection.PokemonWithLegacyMoves pokemonWithLegacyMoves in legacyMoves.Pokemon)
			{
				var targetPokemon = pokemon[pokemonWithLegacyMoves.SpeciesId];

				targetPokemon.FastMoves.AddRange(pokemonWithLegacyMoves.LegacyFastMoves);
				targetPokemon.FastMoves = targetPokemon.FastMoves.Select(m => m).OrderBy(m => m).ToList();

				targetPokemon.ChargedMoves.AddRange(pokemonWithLegacyMoves.LegacyChargeMoves);
				targetPokemon.ChargedMoves = targetPokemon.ChargedMoves.Select(m => m).OrderBy(m => m).ToList();

				targetPokemon.LegacyMoves.AddRange(pokemonWithLegacyMoves.LegacyFastMoves.Concat(pokemonWithLegacyMoves.LegacyChargeMoves).Distinct().OrderBy(m => m));
			}

			IEnumerable<IGrouping<int, PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty>> multiformPokemon = pokemon.Values.GroupBy(p => p.Dex).Where(g => g.Count() > 1);

			var genericEntries = multiformPokemon.Select(g => g.First());

			foreach (PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty genericEntry in genericEntries)
			{
				pokemon.Remove(genericEntry.SpeciesId);
			}

			ExpandHiddenPower(pokemon.Values);
			PruneEmptyLegacyMoves(pokemon);

			return pokemon.Values;
		}

		private static void PruneEmptyLegacyMoves(Dictionary<string, PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty> pokemon)
		{
			foreach (var pokemonProperty in pokemon.Values.Where(p => !p.LegacyMoves.Any()))
			{
				pokemonProperty.LegacyMoves = null;
			}
		}

		private static void ExpandHiddenPower(IEnumerable<PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty> pokemon)
		{
			const string hiddenPower = "HIDDEN_POWER";

			pokemon = pokemon.ToList();

			foreach (var pokemonProperty in pokemon.Where(p => p.FastMoves.Contains(hiddenPower)))
			{
				pokemonProperty.FastMoves = ExpandHiddenPower(pokemonProperty.FastMoves);
			}

			foreach (var pokemonProperty in pokemon.Where(p => p.ChargedMoves.Contains(hiddenPower)))
			{
				pokemonProperty.ChargedMoves = ExpandHiddenPower(pokemonProperty.ChargedMoves);
			}

			foreach (var pokemonProperty in pokemon.Where(p => p.LegacyMoves.Contains(hiddenPower)))
			{
				pokemonProperty.LegacyMoves = ExpandHiddenPower(pokemonProperty.LegacyMoves);
			}

			List<string> ExpandHiddenPower(List<string> moves)
			{
				moves.Remove(hiddenPower);
				moves.AddRange(_hiddenPowers);
				return moves.Distinct().OrderBy(m => m).ToList();
			}
		}

		private static string GenerateSpeciesId(dynamic template)
		{
			string form = (string)template.pokemonSettings.form;

			switch (form)
			{
				case string f when f.EndsWith("ALOLA"):
					form += "N";
					break;
			}

			string speciesId = form ?? (string)template.pokemonSettings.pokemonId;

			switch (speciesId)
			{
				case "NIDORAN_MALE":
					speciesId = "NIDORAN♂";
					break;
				case "NIDORAN_FEMALE":
					speciesId = "NIDORAN♀";
					break;
				case "FARFETCHD":
					speciesId = "FARFETCH'D";
					break;
				case "MR_MIME":
					speciesId = "MR. MIME";
					break;
			}

			return speciesId.ToLower();
		}

		private static string GenerateSpeciesName(string speciesId)
		{
			switch (speciesId)
			{
				case "ho_oh":
				case "porygon_z":
					speciesId = speciesId.Replace('_', '-');
					break;
			}

			if (speciesId.IndexOf('_') >= 0)
			{
				var parts = speciesId.Split('_').Select(part => part.ToUpperFirstCharacter()).ToArray();
				return $"{parts[0]} ({parts[1]})";
			}

			if (speciesId.IndexOf('-') >= 0)
			{
				var parts = speciesId.Split('-').Select(part => part.ToUpperFirstCharacter()).ToArray();
				return $"{parts[0]}-{parts[1]}";
			}

			return speciesId.ToUpperFirstCharacter();
		}

		private static string FormatType(string type)
		{
			if (String.IsNullOrEmpty(type))
			{
				return "none";
			}

			return type.ToLower().Substring("POKEMON_TYPE_".Length);
		}

		public static IEnumerable<PvPokeGameMasterFileManager.GameMasterFile.MovesProperty> AdaptMoves(dynamic gameMaster)
		{
			var regex = new Regex(@"^COMBAT_V\d+_MOVE_");
			//var regex = new Regex(@"^COMBAT_V0250_MOVE_VOLT_SWITCH_FAST");
			//var regex = new Regex(@"^COMBAT_V0296_MOVE_FRENZY_PLANT");
			var templates = ((IEnumerable<dynamic>)gameMaster.itemTemplates).Where(t => regex.IsMatch((string)t.templateId));

			var moves = new List<PvPokeGameMasterFileManager.GameMasterFile.MovesProperty>();

			foreach (dynamic template in templates)
			{
				string moveId = GenerateMoveId((string)template.combatMove.uniqueId);
				int? energyDelta = template.combatMove.energyDelta;
				int? turnCount = Int32.TryParse((string)template.combatMove.durationTurns, out int i) ? i : (int?)null;

				moves.Add(new PvPokeGameMasterFileManager.GameMasterFile.MovesProperty
				{
					MoveId = moveId,
					Name = GenerateMoveName(moveId),
					Type = ((string)template.combatMove.type).Substring("POKEMON_TYPE_".Length).ToLower(),
					Power = (int?)template.combatMove.power ?? 0,
					Energy = energyDelta != null ? (energyDelta < 0 ? Math.Abs((int)energyDelta) : 0) : 0,
					EnergyGain = (int)(energyDelta != null ? (energyDelta > 0 ? energyDelta : 0) : 0),
					Cooldown = (turnCount + 1) * 500 * 2 // the additional * 2 is because of a bug in pvpoke where it expects the duration to be twice as long as it should be -- this operand can be removed if/when that bug is fixed
				});
			}

			return moves.Select(m => m).OrderBy(m => m.Name);
		}

		private static string GenerateMoveId(string moveId)
		{
			switch (moveId)
			{
				case "FUTURESIGHT":
					return "FUTURE_SIGHT";
				case string m when m.EndsWith("_FAST"):
					return moveId.Replace("_FAST", String.Empty);
				default:
					return moveId;
			}
		}

		private static string GenerateMoveName(string moveId)
		{
			switch (moveId)
			{
				case "X_SCISSOR":
					return "X-Scissor";
				default:
					return String.Join(' ', moveId.ToLower().Split('_').Select(word => word.ToUpperFirstCharacter()));
			}
		}
	}

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