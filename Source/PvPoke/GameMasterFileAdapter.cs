using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PvPoke.FileManagement;
using PvPoke.FileManagement.PokemonGo;
using PvPoke.FileManagement.PvPoke;

namespace PvPoke
{
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
						Hp = template.pokemonSettings.stats.baseStamina
					},
					Types = new List<string>
					{
						FormatType((string)template.pokemonSettings.type),
						FormatType((string)template.pokemonSettings.type2)
					},
					FastMoves = template.pokemonSettings.quickMoves != null ? new List<string>(((IEnumerable<string>)template.pokemonSettings.quickMoves.ToObject<IEnumerable<string>>()).Select(GenerateMoveId).Distinct().OrderBy(m => m)) : new List<string>(),
					ChargedMoves = template.pokemonSettings.cinematicMoves != null ? new List<string>(((IEnumerable<string>)template.pokemonSettings.cinematicMoves.ToObject<IEnumerable<string>>()).Select(GenerateMoveId).Distinct().OrderBy(m => m)) : new List<string>(),
					LegacyMoves = new List<string>()
				};

				pokemon[speciesId] = pokemonProperty;
			}

			RemoveSubtypePokemon(pokemon, "_shadow");
			RemoveSubtypePokemon(pokemon, "_purified");
			RemoveBaseTypeIfNormalTypeExists(pokemon);

			RenameNormalFormPokemon(pokemon);
			await AddMissingPokemonAsync(pokemon);

			var legacyMovesJson = await FileManager.ReadFileAsync(PokemonGoGameMasterFileManager.LegacyMovesJsonPath);
			var legacyMoves = FileManager.LoadFile<LegacyMoveCollection>(legacyMovesJson);

			foreach (LegacyMoveCollection.PokemonWithLegacyMoves pokemonWithLegacyMoves in legacyMoves.Pokemon)
			{
				var targetPokemon = pokemon[pokemonWithLegacyMoves.SpeciesId];

				targetPokemon.FastMoves.AddRange(pokemonWithLegacyMoves.LegacyFastMoves);
				targetPokemon.FastMoves = targetPokemon.FastMoves.OrderBy(m => m).ToList();

				targetPokemon.ChargedMoves.AddRange(pokemonWithLegacyMoves.LegacyChargeMoves);
				targetPokemon.ChargedMoves = targetPokemon.ChargedMoves.OrderBy(m => m).ToList();

				targetPokemon.LegacyMoves.AddRange(pokemonWithLegacyMoves.LegacyFastMoves.Concat(pokemonWithLegacyMoves.LegacyChargeMoves).Distinct().OrderBy(m => m));
			}

			ExpandHiddenPower(pokemon.Values);
			PruneEmptyLegacyMoves(pokemon);

			return pokemon.Values.OrderBy(p => p.Dex).ThenBy(p => p.SpeciesId);
		}

		private static void RemoveSubtypePokemon(Dictionary<string, PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty> pokemon, string subtypeSuffix)
		{
			IEnumerable<IGrouping<int, PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty>> multiformPokemon = pokemon.Values.GroupBy(p => p.Dex).Where(g => g.Count() > 1);

			foreach (PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty subtype in multiformPokemon.SelectMany(g => g.Where(p => p.SpeciesId.EndsWith(subtypeSuffix))))
			{
				pokemon.Remove(subtype.SpeciesId);
			}
		}

		private static void RemoveBaseTypeIfNormalTypeExists(Dictionary<string, PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty> pokemon)
		{
			IEnumerable<IGrouping<int, PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty>> multiformPokemon = pokemon.Values.GroupBy(p => p.Dex).Where(g => g.Count() > 1);

			foreach (IGrouping<int, PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty> dexEntryGrouping in multiformPokemon)
			{
				PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty normalType = dexEntryGrouping.SingleOrDefault(p => p.SpeciesId.EndsWith("_normal"));

				if (normalType != null)
				{
					var baseType = dexEntryGrouping.SingleOrDefault(p => p.SpeciesId == normalType.SpeciesId.Replace("_normal", String.Empty));

					if (baseType != null)
					{
						pokemon.Remove(baseType.SpeciesId);
					}
				}
			}
		}

		private static void RenameNormalFormPokemon(Dictionary<string, PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty> pokemon)
		{
			IEnumerable<IGrouping<int, PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty>> multiformPokemon = pokemon.Values.GroupBy(p => p.Dex).Where(g => g.Count() > 1);
			IEnumerable<PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty> normalForms = multiformPokemon.SelectMany(g => g.Where(p => p.SpeciesId.EndsWith("_normal")));

			// handles normals vs alolans
			foreach (PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty normalForm in normalForms)
			{
				CleanPokemonData(pokemon, normalForm);
			}

			// handles normals leftover from purified/shadow pokemon
			IEnumerable<PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty> remainingNormalForms = pokemon.Values.Where(p => p.SpeciesId.EndsWith("_normal")).ToList();
			foreach (var normalForm in remainingNormalForms)
			{
				CleanPokemonData(pokemon, normalForm);
			}

			void CleanPokemonData(Dictionary<string, PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty> pokemonCollection, PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty normalForm)
			{
				string oldSpeciesId = normalForm.SpeciesId;
				string newSpeciesId = oldSpeciesId.Replace("_normal", String.Empty);

				normalForm.SpeciesId = newSpeciesId;
				normalForm.SpeciesName = normalForm.SpeciesName.Replace(" (Normal)", String.Empty);
				pokemonCollection.Add(normalForm.SpeciesId, normalForm);

				pokemonCollection.Remove(oldSpeciesId);
			}
		}

		private static async Task AddMissingPokemonAsync(Dictionary<string, PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty> pokemon)
		{
			foreach (var filePath in Directory.EnumerateFiles(PokemonGoGameMasterFileManager.MissingPokemonFromGameMasterPath))
			{
				var json = await FileManager.ReadFileAsync(filePath);
				var missingPokemon = FileManager.LoadFile<PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty>(json);

				if (!pokemon.ContainsKey(missingPokemon.SpeciesId))
				{
					missingPokemon.LegacyMoves = new List<string>();
					pokemon.Add(missingPokemon.SpeciesId, missingPokemon);
				}
			}
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
			string speciesId = (string)template.pokemonSettings.form ?? (string)template.pokemonSettings.pokemonId;

			switch (speciesId)
			{
				case string s when s.EndsWith("ALOLA"):
					speciesId += "N";
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
				case "mr_mime":
					speciesId = "Mr. Mime";
					break;
				case "nidoran_male":
					speciesId = "Nidoran♂";
					break;
				case "nidoran_female":
					speciesId = "Nidoran♀";
					break;
				case "farfetchd":
					speciesId = "Farfetch'd";
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
				int turnCount = Int32.TryParse((string)template.combatMove.durationTurns, out int i) ? i : 0;

				moves.Add(new PvPokeGameMasterFileManager.GameMasterFile.MovesProperty
				{
					MoveId = moveId,
					Name = GenerateMoveName(moveId),
					Type = ((string)template.combatMove.type).Substring("POKEMON_TYPE_".Length).ToLower(),
					Power = (int?)template.combatMove.power ?? 0,
					Energy = energyDelta != null ? (energyDelta < 0 ? Math.Abs((int)energyDelta) : 0) : 0,
					EnergyGain = (int)(energyDelta != null ? (energyDelta > 0 ? energyDelta : 0) : 0),
					Cooldown = (turnCount + 1) * 500
				});
			}

			var genericHiddenPower = moves.Single(m => m.MoveId == "HIDDEN_POWER");
			moves.AddRange(GenerateHiddenPowerMoves(genericHiddenPower));
			moves.Remove(genericHiddenPower);

			return moves.OrderBy(m => m.Name);
		}

		private static IEnumerable<PvPokeGameMasterFileManager.GameMasterFile.MovesProperty> GenerateHiddenPowerMoves(PvPokeGameMasterFileManager.GameMasterFile.MovesProperty genericHiddenPower)
		{
			foreach (string hiddenPower in _hiddenPowers)
			{
				yield return new PvPokeGameMasterFileManager.GameMasterFile.MovesProperty
				{
					MoveId = hiddenPower,
					Name = GenerateMoveName(hiddenPower),
					Type = hiddenPower.Split('_')[2].ToLower(),
					Power = genericHiddenPower.Power,
					Energy = genericHiddenPower.Energy,
					EnergyGain = genericHiddenPower.EnergyGain,
					Cooldown = genericHiddenPower.Cooldown
				};
			}
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
				case string p when p.StartsWith("HIDDEN_POWER"):
					string[] parts = moveId.ToLower().Split('_').Select(word => word.ToUpperFirstCharacter()).ToArray();
					string move = $"{parts[0]} {parts[1]}";
					if (parts.Length == 3)
					{
						move = $"{move} ({parts[2]})";
					}

					return move;
				case "X_SCISSOR":
					return "X-Scissor";
				default:
					return String.Join(' ', moveId.ToLower().Split('_').Select(word => word.ToUpperFirstCharacter()));
			}
		}
	}
}