using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CsvHelper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

		[Fact(Skip = "Refresh PvPoke game master")]
		public async Task FormatJsonFiles()
		{
			await PvPokeGameMasterFileManager.FetchAndSaveFileAsync();

			foreach (string filePath in Directory.EnumerateFiles(PokemonGoGameMasterFileManager.DataPath).Where(f => f.EndsWith(".json")))
			{
				var json = await FileManager.ReadFileAsync(filePath);
				string jsonFormatted = JToken.Parse(json).ToString(Formatting.Indented);
				await FileManager.SaveFileAsync(jsonFormatted, filePath);
			}
		}

		[Fact]
		public async Task RoundTripPvPokeJson()
		{
			PvPokeGameMasterFileManager.GameMasterFile file = await PvPokeGameMasterFileManager.LoadFileAsync(PvPokeGameMasterFileManager.GeneratedPvPokeGameMasterJsonPath);

			foreach (var pokemonProperty in file.Pokemon)
			{
				pokemonProperty.FastMoves = pokemonProperty.FastMoves.OrderBy(m => m).ToList();
				pokemonProperty.ChargedMoves = pokemonProperty.ChargedMoves.OrderBy(m => m).ToList();
				if (pokemonProperty.LegacyMoves != null)
				{
					pokemonProperty.LegacyMoves = pokemonProperty.LegacyMoves.OrderBy(m => m).ToList();
				}
			}

			file.Pokemon = file.Pokemon.OrderBy(p => p.Dex).ThenBy(p => p.SpeciesId);
			file.Moves = file.Moves.OrderBy(m => m.Name);

			string serializedFile = file.ToJson();
			_output.WriteLine(serializedFile);
			//Assert.Equal(json, serializedFile);
		}

		//[Fact(Skip = "Assert that we can round trip the raw json")]
		[Fact]
		public async Task GeneratePvpokeGameMasterJson()
		{
			//await PokemonGoGameMasterFileManager.FetchAndSaveFileAsync();
			var json = await PokemonGoGameMasterFileManager.ReadFileAsync(PokemonGoGameMasterFileManager.GameMasterJsonPath);
			dynamic gameMaster = JsonConvert.DeserializeObject<dynamic>(json);

			var settings = await FileManager.LoadFileAsync<dynamic>(PokemonGoGameMasterFileManager.SettingsJsonPath);
			var cups = await FileManager.LoadFileAsync<dynamic>(PokemonGoGameMasterFileManager.CupsJsonPath);

			var gameMasterFile = new PvPokeGameMasterFileManager.GameMasterFile
			{
				Settings = settings,
				Cups = cups,
				Pokemon = await GameMasterFileAdapter.AdaptPokemonAsync(gameMaster),
				Moves = GameMasterFileAdapter.AdaptMoves(gameMaster)
			};

			string gameMasterJson = JsonConvert.SerializeObject(gameMasterFile, _jsonSerializerSettings);
			await FileManager.SaveFileAsync(gameMasterJson, PokemonGoGameMasterFileManager.GeneratedPvPokeGameMasterJsonPath);
			_output.WriteLine(gameMasterJson);
		}

		[Fact]
		public async Task GenerateLegacyMovesJson()
		{
			var json = await PokemonGoGameMasterFileManager.ReadFileAsync(PokemonGoGameMasterFileManager.GameMasterJsonPath);
			dynamic gameMaster = JsonConvert.DeserializeObject<dynamic>(json);
			var regex = new Regex(@"^COMBAT_V\d+_MOVE_");
			var templates = ((IEnumerable<dynamic>)gameMaster.itemTemplates).Where(t => regex.IsMatch((string)t.templateId));

			var moves = new Dictionary<string, bool>();

			foreach (dynamic template in templates)
			{
				string moveId = (string)template.combatMove.uniqueId;
				moves.Add(moveId.Replace("_FAST", String.Empty), moveId.EndsWith("_FAST"));
			}

			var pvpokeJson = await PvPokeGameMasterFileManager.ReadFileAsync(PvPokeGameMasterFileManager.ActualPvPokeGameMasterJsonPath);
			var pvpokeGameMaster = JsonConvert.DeserializeObject<PvPokeGameMasterFileManager.GameMasterFile>(pvpokeJson);
			var legacyMoveCollection = new LegacyMoveCollection();

			foreach (var pokemon in pvpokeGameMaster.Pokemon)
			{
				var pokemonWithLegacyMoves = new LegacyMoveCollection.PokemonWithLegacyMoves {SpeciesId = pokemon.SpeciesId.Replace("_normal", String.Empty)};

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

			string legacyMovesJson = JsonConvert.SerializeObject(legacyMoveCollection, _jsonSerializerSettings);
			await FileManager.SaveFileAsync(legacyMovesJson, PokemonGoGameMasterFileManager.LegacyMovesJsonPath);
			_output.WriteLine(legacyMovesJson);
		}

		[Fact]
		public async Task GenerateDefaultIVsJson()
		{
			var pvpokeJson = await PvPokeGameMasterFileManager.ReadFileAsync(PvPokeGameMasterFileManager.ActualPvPokeGameMasterJsonPath);
			var pvpokeGameMaster = JsonConvert.DeserializeObject<PvPokeGameMasterFileManager.GameMasterFile>(pvpokeJson);

			var pokemonDefaultIVs = new DefaultIVsCollection();

			foreach (PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty pokemon in pvpokeGameMaster.Pokemon)
			{
				pokemonDefaultIVs.Pokemon[pokemon.SpeciesId] = new Dictionary<string, List<decimal>>();
				foreach (KeyValuePair<string, List<decimal>> pokemonDefaultIV in pokemon.DefaultIVs)
				{
					pokemonDefaultIVs.Pokemon[pokemon.SpeciesId][pokemonDefaultIV.Key] = pokemon.DefaultIVs[pokemonDefaultIV.Key];
				}
			}

			string defaultIVsJson = JsonConvert.SerializeObject(pokemonDefaultIVs, _jsonSerializerSettings);
			await FileManager.SaveFileAsync(defaultIVsJson, PokemonGoGameMasterFileManager.DefaultIVsJsonPath);
			_output.WriteLine(defaultIVsJson);
		}

		[Fact]
		public async Task GenerateFastMovesCsv()
		{
			var pvpokeJson = await PvPokeGameMasterFileManager.ReadFileAsync(PvPokeGameMasterFileManager.GeneratedPvPokeGameMasterJsonPath);
			var pvpokeGameMaster = JsonConvert.DeserializeObject<PvPokeGameMasterFileManager.GameMasterFile>(pvpokeJson);

			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer))
			{
				var fastMoves = pvpokeGameMaster.Moves.Where(m => m.EnergyGain > 0).Select(m => new
				{
					Move = m.Name,
					m.Power,
					m.EnergyGain,
					Turns = m.Cooldown / 500,
					Type = m.Type.ToUpperFirstCharacter()
				});
				csv.WriteRecords(fastMoves);
				_output.WriteLine(writer.ToString());
			}
		}

		[Fact]
		public async Task GenerateChargeMovesCsv()
		{
			var pvpokeJson = await PvPokeGameMasterFileManager.ReadFileAsync(PvPokeGameMasterFileManager.GeneratedPvPokeGameMasterJsonPath);
			var pvpokeGameMaster = JsonConvert.DeserializeObject<PvPokeGameMasterFileManager.GameMasterFile>(pvpokeJson);

			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer))
			{
				var chargeMoves = pvpokeGameMaster.Moves.Where(m => m.Energy > 0).Select(m => new
				{
					Move = m.Name,
					m.Power,
					m.Energy,
					Type = m.Type.ToUpperFirstCharacter()
				});
				csv.WriteRecords(chargeMoves);
				_output.WriteLine(writer.ToString());
			}
		}
	}
}