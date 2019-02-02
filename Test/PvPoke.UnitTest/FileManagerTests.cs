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

		[Fact(Skip = "Assert that we can round trip the raw json")]
		public async Task RoundTripPvPokeJson()
		{
			var json = await PvPokeGameMasterFileManager.ReadFileAsync();
			var file = PvPokeGameMasterFileManager.LoadFile(json);
			string serializedFile = file.ToJson();
			_output.WriteLine(serializedFile);
			Assert.Equal(json, serializedFile);
		}

		//[Fact(Skip = "Assert that we can round trip the raw json")]
		[Fact]
		public async Task GeneratePvpokeGameMaster()
		{
			//await PokemonGoGameMasterFileManager.FetchAndSaveFileAsync();
			var json = await PokemonGoGameMasterFileManager.ReadFileAsync();
			dynamic gameMaster = JsonConvert.DeserializeObject<dynamic>(json);

			var optionsJson = await FileManager.ReadFileAsync(Path.Combine(AppContext.BaseDirectory, "Data", "options.json"));
			var options = FileManager.LoadFile<PvPokeGameMasterFileManager.GameMasterFile.OptionsProperty>(optionsJson);

			var cupsJson = await FileManager.ReadFileAsync(Path.Combine(AppContext.BaseDirectory, "Data", "cups.json"));
			var cups = FileManager.LoadFile<PvPokeGameMasterFileManager.GameMasterFile.CupsProperty>(cupsJson);

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
			var json = await PvPokeGameMasterFileManager.ReadFileAsync();
			var gameMaster = JsonConvert.DeserializeObject<PvPokeGameMasterFileManager.GameMasterFile>(json);
			var legacyMoveCollection = new LegacyMoveCollection();

			foreach (var pokemon in gameMaster.Pokemon)
			{
				var pokemonWithLegacyMoves = new LegacyMoveCollection.PokemonWithLegacyMoves {SpeciesId = pokemon.SpeciesId};

				if (pokemon.LegacyMoves != null)
				{
					foreach (string legacyMove in pokemon.LegacyMoves)
					{
						if (pokemon.FastMoves.Any(f => f == legacyMove))
						{
							pokemonWithLegacyMoves.LegacyFastMoves.Add(legacyMove);
						}

						if (pokemon.ChargedMoves.Any(c => c == legacyMove))
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
					SpeciesName = speciesId.ToUpperFirstCharacter(),
					SpeciesId = speciesId,
					BaseStats = new PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty.BaseStatsProperty
					{
						Atk = template.pokemonSettings.stats.baseAttack,
						Def = template.pokemonSettings.stats.baseDefense,
						Hp = template.pokemonSettings.stats.baseStamina,
					},
					Types = new List<string> {FormatType((string)template.pokemonSettings.type)},
					FastMoves = new List<string>(((IEnumerable<string>)template.pokemonSettings.quickMoves.ToObject<IEnumerable<string>>()).Select(m => m.Remove(m.Length - "_FAST".Length))),
					ChargedMoves = new List<string>(((IEnumerable<string>)template.pokemonSettings.cinematicMoves.ToObject<IEnumerable<string>>()).Select(m => m)),
					LegacyMoves = new List<string>()
				};

				string type2 = (string)template.pokemonSettings.type2;
				if (!String.IsNullOrEmpty(type2))
				{
					pokemonProperty.Types.Add(FormatType(type2));
				}

				pokemon.Add(speciesId, pokemonProperty);
			}

			var legacyMovesJson = await FileManager.ReadFileAsync(Path.Combine(AppContext.BaseDirectory, "Data", "legacyMoves.json"));
			var legacyMoves = FileManager.LoadFile<LegacyMoveCollection>(legacyMovesJson);

			foreach (LegacyMoveCollection.PokemonWithLegacyMoves pokemonWithLegacyMoves in legacyMoves.Pokemon)
			{
				var targetPokemon = pokemon[pokemonWithLegacyMoves.SpeciesId];
				targetPokemon.FastMoves.AddRange(pokemonWithLegacyMoves.LegacyFastMoves);
				targetPokemon.ChargedMoves.AddRange(pokemonWithLegacyMoves.LegacyChargeMoves);
				targetPokemon.LegacyMoves.AddRange(pokemonWithLegacyMoves.LegacyFastMoves.Concat(pokemonWithLegacyMoves.LegacyChargeMoves));
			}

			return pokemon.Values;
		}

		private static string GenerateSpeciesId(dynamic template)
		{
			string form = (string)template.pokemonSettings.form;

			if (!String.IsNullOrEmpty(form) && form.EndsWith("ALOLA"))
			{
				form += "N";
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
				case "HO_OH":
					speciesId = "HO-OH";
					break;
			}

			return speciesId.ToLower();
		}

		private static string FormatType(string input)
		{
			return input.ToLower().Substring("POKEMON_TYPE_".Length);
		}

		public static IEnumerable<PvPokeGameMasterFileManager.GameMasterFile.MovesProperty> AdaptMoves(dynamic gameMaster)
		{
			var regex = new Regex(@"^COMBAT_V\d+_MOVE_");
			//var regex = new Regex(@"^COMBAT_V0250_MOVE_VOLT_SWITCH_FAST");
			//var regex = new Regex(@"^COMBAT_V0296_MOVE_FRENZY_PLANT");
			var templates = ((IEnumerable<dynamic>)gameMaster.itemTemplates).Where(t => regex.IsMatch((string)t.templateId));

			foreach (dynamic template in templates)
			{
				string moveId = ((string)template.combatMove.uniqueId).Replace("_FAST", String.Empty);
				int? energyDelta = template.combatMove.energyDelta;
				int? turnCount = Int32.TryParse((string)template.combatMove.durationTurns, out int i) ? i : (int?)null;

				yield return new PvPokeGameMasterFileManager.GameMasterFile.MovesProperty
				{
					MoveId = moveId,
					Name = String.Join(' ', moveId.ToLower().Split('_').Select(word => word.ToUpperFirstCharacter())),
					Type = ((string)template.combatMove.type).Substring("POKEMON_TYPE_".Length).ToLower(),
					Power = (int?)template.combatMove.power ?? 0,
					Energy = energyDelta != null ? (energyDelta < 0 ? Math.Abs((int)energyDelta) : 0) : 0,
					EnergyGain = (int)(energyDelta != null ? (energyDelta > 0 ? energyDelta : 0) : 0),
					Cooldown = (turnCount + 1) * 500 * 2 // the additional * 2 is because of a bug in pvpoke where it expects the duration to be twice as long as it should be -- this operand can be removed if/when that bug is fixed
				};
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