using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PvPoke.FileManagement.PokemonGo
{
	public static class PokemonGoGameMasterFileManager
	{
		private static readonly string _gameMasterVersionUri = "https://raw.githubusercontent.com/pokemongo-dev-contrib/pokemongo-game-master/master/versions/latest-version.txt";
		private static readonly string _gameMasterJsonUri = "https://raw.githubusercontent.com/pokemongo-dev-contrib/pokemongo-game-master/master/versions/latest/GAME_MASTER.json";

		public static string DataPath
		{
			get
			{
				const string rootFolder = "pvpoke-gamemaster-adapter";
				var path = AppContext.BaseDirectory.Substring(0, AppContext.BaseDirectory.LastIndexOf(rootFolder, StringComparison.Ordinal) + rootFolder.Length);
				return Path.Combine(path, "Source", "PvPoke", "Data");
			}
		}

		public static string GameMasterJsonPath => Path.Combine(DataPath, "gameMaster.json");
		public static string CupsJsonPath => Path.Combine(DataPath, "cups.json");
		public static string LegacyMovesJsonPath => Path.Combine(DataPath, "legacyMoves.json");
		public static string SettingsJsonPath => Path.Combine(DataPath, "settings.json");
		public static string ActualPvPokeGameMasterJsonPath => Path.Combine(DataPath, "actualPvPokeGameMaster.json");
		public static string GeneratedPvPokeGameMasterJsonPath => Path.Combine(DataPath, "generatedPvPokeGameMaster.json");

		public static bool FileExists()
		{
			return FileManager.FileExists(GameMasterJsonPath);
		}

		public static async Task<long> FetchLatestVersionAsync()
		{
			Console.WriteLine("Fetching latest version #...");
			return Convert.ToInt64(await FileManager.FetchFileAsync(_gameMasterVersionUri));
		}

		public static async Task<string> ReadFileAsync(string filePath)
		{
			return await FileManager.ReadFileAsync(filePath);
		}

		public static GameMasterFile LoadFile(string json)
		{
			return FileManager.LoadFile<GameMasterFile>(json);
		}

		public static async Task<GameMasterFile> FetchAndSaveFileAsync()
		{
			string json = await FileManager.FetchFileAsync(_gameMasterJsonUri);
			await FileManager.SaveFileAsync(json, GameMasterJsonPath);
			return FileManager.LoadFile<GameMasterFile>(json);
		}

		public class GameMasterFile
		{
			private static readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
			{
				ContractResolver = new CamelCasePropertyNamesContractResolver(),
				NullValueHandling = NullValueHandling.Ignore
			};

			public IEnumerable<ItemTemplatesProperty> ItemTemplates { get; set; }
			public long TimeStampMs { get; set; }

			public class ItemTemplatesProperty
			{
				public string TemplateId { get; set; }
				public AvatarCustomizationProperty AvatarCustomization { get; set; }
				public BackgroundModeSettingsProperty BackgroundModeSettings { get; set; }
				public BadgeSettingsProperty BadgeSettings { get; set; }
				public BattleSettingsProperty BattleSettings { get; set; }
				public BelugaPokemonWhitelistProperty BelugaPokemonWhitelist { get; set; }
				public CombatLeagueProperty CombatLeague { get; set; }
				public CombatLeagueSettingsProperty CombatLeagueSettings { get; set; }
				public CombatSettingsProperty CombatSettings { get; set; }
				public CombatStatStageSettingsProperty CombatStatStageSettings { get; set; }
				public CombatMoveProperty CombatMove { get; set; }
				public EncounterSettingsProperty EncounterSettings { get; set; }
				public ExRaidSettingsProperty ExRaidSettings { get; set; }
				public FormSettingsProperty FormSettings { get; set; }

				public class AvatarCustomizationProperty
				{
					public bool? Enabled { get; set; }
					public string AvatarType { get; set; }
					public IEnumerable<string> Slot { get; set; }
					public string BundleName { get; set; }
					public string AssetName { get; set; }
					public string GroupName { get; set; }
					public int SortOrder { get; set; }
					public string UnlockType { get; set; }
					public string UnlockBadgeType { get; set; }
					public string IapSku { get; set; }
					public int? UnlockBadgeLevel { get; set; }
					public string IconName { get; set; }
					public int? UnlockPlayerLevel { get; set; }
				}

				public class BackgroundModeSettingsProperty
				{
					public decimal WeeklyFitnessGoalLevel1DistanceKm { get; set; }
					public decimal WeeklyFitnessGoalLevel2DistanceKm { get; set; }
					public decimal WeeklyFitnessGoalLevel3DistanceKm { get; set; }
				}

				public class BadgeSettingsProperty
				{
					public string BadgeType { get; set; }
					public int BadgeRank { get; set; }
					public IEnumerable<int> Targets { get; set; }
					public bool? EventBadge { get; set; }
				}

				public class BattleSettingsProperty
				{
					public decimal RetargetSeconds { get; set; }
					public decimal EnemyAttackInterval { get; set; }
					public decimal AttackServerInterval { get; set; }
					public decimal RoundDurationSeconds { get; set; }
					public decimal BonusTimePerAllySeconds { get; set; }
					public int MaximumAttackersPerBattle { get; set; }
					public decimal SameTypeAttackBonusMultiplier { get; set; }
					public int MaximumEnergy { get; set; }
					public decimal EnergyDeltaPerHealthLost { get; set; }
					public int DodgeDurationMs { get; set; }
					public int MinimumPlayerLevel { get; set; }
					public int SwapDurationMs { get; set; }
					public decimal DodgeDamageReductionPercent { get; set; }
					public int MinimumRaidPlayerLevel { get; set; }
				}

				public class BelugaPokemonWhitelistProperty
				{
					public int MaxAllowedPokemonPokedexNumber { get; set; }
					public IEnumerable<string> AdditionalPokemonAllowed { get; set; }
					public IEnumerable<string> FormsAllowed { get; set; }
					public IEnumerable<string> CostumesAllowed { get; set; }
				}

				public class CombatLeagueProperty
				{
					public string Title { get; set; }
					public bool Enabled { get; set; }
					public IEnumerable<UnlockConditionProperty> UnlockCondition { get; set; }
					public IEnumerable<PokemonConditionProperty> PokemonCondition { get; set; }
					public string IconUrl { get; set; }
					public int PokemonCount { get; set; }
					public IEnumerable<string> BannedPokemon { get; set; }
					public string BadgeType { get; set; }

					public class UnlockConditionProperty
					{
						public string Type { get; set; }
						public int MinPokemonCount { get; set; }
						public WithPokemonCpLimitProperty WithPokemonCpLimit { get; set; }
					}

					public class PokemonConditionProperty
					{
						public string Type { get; set; }
						public WithPokemonCpLimitProperty WithPokemonCpLimit { get; set; }
					}

					public class WithPokemonCpLimitProperty
					{
						public int? MinCp { get; set; }
						public int MaxCp { get; set; }
					}
				}

				public class CombatLeagueSettingsProperty
				{
					public IEnumerable<string> CombatLeagueTemplateId { get; set; }
				}

				public class CombatSettingsProperty
				{
					public decimal RoundDurationSeconds { get; set; }
					public decimal TurnDurationSeconds { get; set; }
					public decimal MinigameDurationSeconds { get; set; }
					public decimal SameTypeAttackBonusMultiplier { get; set; }
					public decimal FastAttackBonusMultiplier { get; set; }
					public decimal ChargeAttackBonusMultiplier { get; set; }
					public decimal DefenseBonusMultiplier { get; set; }
					public decimal MinigameBonusBaseMultiplier { get; set; }
					public decimal MinigameBonusVariableMultiplier { get; set; }
					public int MaxEnergy { get; set; }
					public decimal DefenderMinigameMultiplier { get; set; }
					public decimal ChangePokemonDurationSeconds { get; set; }
					public decimal MinigameSubmitScoreDurationSeconds { get; set; }
					public decimal QuickSwapCooldownDurationSeconds { get; set; }
				}

				public class CombatStatStageSettingsProperty
				{
					public int MinimumStatStage { get; set; }
					public int MaximumStatStage { get; set; }
					public IEnumerable<decimal> AttackBuffMultiplier { get; set; }
					public IEnumerable<decimal> DefenseBuffMultiplier { get; set; }
				}

				public class CombatMoveProperty
				{
					public string UniqueId { get; set; }
					public string Type { get; set; }
					public decimal? Power { get; set; }
					public string VfxName { get; set; }
					public int? DurationTurns { get; set; }
					public int? EnergyDelta { get; set; }
				}

				public class EncounterSettingsProperty
				{
					public decimal SpinBonusThreshold { get; set; }
					public decimal ExcellentThrowThreshold { get; set; }
					public decimal GreatThrowThreshold { get; set; }
					public decimal NiceThrowThreshold { get; set; }
					public int MilestoneThreshold { get; set; }
					public bool ArPlusModeEnabled { get; set; }
					public decimal ArCloseProximityThreshold { get; set; }
					public decimal ArLowAwarenessThreshold { get; set; }
				}

				public class ExRaidSettingsProperty
				{
					public string MinimumExRaidShareLevel { get; set; }
				}

				public class FormSettingsProperty
				{
					public string Pokemon { get; set; }
					public IEnumerable<FormsProperty> Forms { get; set; }

					public class FormsProperty
					{
						public string Form { get; set; }
						public int? AssetBundleValue { get; set; }
					}
				}
			}

			public string ToJson()
			{
				return JsonConvert.SerializeObject(this, Formatting.Indented, _jsonSerializerSettings);
			}
		}
	}
}