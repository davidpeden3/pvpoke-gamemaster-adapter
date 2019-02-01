using System;
using System.Collections.Generic;
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
        public async Task RoundTripPokemonGoJson()
        {
            //await PokemonGoGameMasterFileManager.FetchAndSaveFileAsync();
            var json = await PokemonGoGameMasterFileManager.ReadFileAsync();
            dynamic gameMaster = JsonConvert.DeserializeObject<dynamic>(json);

            var gameMasterFile = new PvPokeGameMasterFileManager.GameMasterFile
            {
                Pokemon = GameMasterFileAdapter.AdaptPokemon(gameMaster),
                Moves = GameMasterFileAdapter.AdaptMoves(gameMaster)
            };

            _output.WriteLine(JsonConvert.SerializeObject(gameMasterFile, _jsonSerializerSettings));
        }
    }

    public static class GameMasterFileAdapter
    {
        public static IEnumerable<PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty> AdaptPokemon(dynamic gameMaster)
        {
            var regex = new Regex(@"^V\d+_POKEMON_");
            var templates = ((IEnumerable<dynamic>)gameMaster.itemTemplates).Select(t => t).Where(t => regex.IsMatch((string)t.templateId));

            foreach (dynamic template in templates)
            {
                yield return new PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty
                {
                    Dex = Convert.ToInt32(((string)template.templateId).Substring(1, 4)),
                    SpeciesName = ((string)template.pokemonSettings.pokemonId).ToLower().ToUpperFirstCharacter(),
                    SpeciesId = ((string)template.pokemonSettings.pokemonId).ToLower(),
                    BaseStats = new PvPokeGameMasterFileManager.GameMasterFile.PokemonProperty.BaseStatsProperty()
                    {
                        Atk = template.pokemonSettings.stats.baseAttack,
                        Def = template.pokemonSettings.stats.baseDefense,
                        Hp = template.pokemonSettings.stats.baseStamina,
                    },
                    Types = new List<string>
                    {
                        ((string)template.pokemonSettings.type).ToLower().Substring("POKEMON_TYPE_".Length),
                        ((string)template.pokemonSettings.type2).ToLower().Substring("POKEMON_TYPE_".Length)
                    },
                    FastMoves = ((IEnumerable<string>)template.pokemonSettings.quickMoves.ToObject<IEnumerable<string>>()).Select(m => m.Remove(m.Length - "_FAST".Length)),
                    ChargedMoves = ((IEnumerable<string>)template.pokemonSettings.cinematicMoves.ToObject<IEnumerable<string>>()).Select(m => m),
                    LegacyMoves = Enumerable.Empty<string>()
                };

                break;
            }
        }

        public static IEnumerable<PvPokeGameMasterFileManager.GameMasterFile.MovesProperty> AdaptMoves(dynamic gameMaster)
        {
            //var regex = new Regex(@"^COMBAT_V\d+_MOVE_");
            var regex = new Regex(@"^COMBAT_V0250_MOVE_VOLT_SWITCH_FAST");
            var templates = ((IEnumerable<dynamic>)gameMaster.itemTemplates).Where(t => regex.IsMatch((string)t.templateId));

            foreach (dynamic template in templates)
            {
                string pvpTemplateId = template.templateId;
                string pveTemplateId = pvpTemplateId.Substring("COMBAT_".Length);
                string moveId = ((string)template.combatMove.uniqueId).Replace("_FAST", String.Empty);
                int energyDelta = template.combatMove.energyDelta;
                dynamic pveMove = ((IEnumerable<dynamic>)gameMaster.itemTemplates).Single(t => t.templateId == pveTemplateId);

                yield return new PvPokeGameMasterFileManager.GameMasterFile.MovesProperty
                {
                    MoveId = moveId,
                    Name = String.Join(' ', moveId.ToLower().Split('_').Select(word => word.ToUpperFirstCharacter())),
                    Type = ((string)template.combatMove.type).Substring("POKEMON_TYPE_".Length).ToLower(),
                    Power = template.combatMove.power,
                    Energy = energyDelta < 0 ? Math.Abs(energyDelta) : 0,
                    EnergyGain = energyDelta > 0 ? energyDelta : 0,
                    DamageWindow = pveMove.moveSettings.damageWindowStartMs,
                    Cooldown = 0
                };

                break;
            }
        }
    }
}