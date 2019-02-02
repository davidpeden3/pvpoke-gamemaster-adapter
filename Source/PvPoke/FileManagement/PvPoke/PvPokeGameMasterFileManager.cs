﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PvPoke.FileManagement.PvPoke
{
    public static class PvPokeGameMasterFileManager
    {
        private static readonly string _pvpokeGameMasterJsonUri = "https://raw.githubusercontent.com/pvpoke/pvpoke/master/src/data/gamemaster.json";

        private static string GameMasterJsonPath => Path.Combine(AppContext.BaseDirectory, "Data", "pvpokeGameMaster.json");

        public static bool FileExists()
        {
            return FileManager.FileExists(GameMasterJsonPath);
        }

        public static async Task<string> ReadFileAsync()
        {
            return await FileManager.ReadFileAsync(GameMasterJsonPath);
        }

        public static GameMasterFile LoadFile(string json)
        {
            return FileManager.LoadFile<GameMasterFile>(json);
        }

        public static async Task<GameMasterFile> FetchAndSaveFileAsync()
        {
            string json = await FileManager.FetchFileAsync(_pvpokeGameMasterJsonUri);
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

            public dynamic Options { get; set; }
            public dynamic Cups { get; set; }
            public IEnumerable<PokemonProperty> Pokemon { get; set; }
            public IEnumerable<MovesProperty> Moves { get; set; }

            public class PokemonProperty
            {
                public int Dex { get; set; }
                public string SpeciesName { get; set; }
                public string SpeciesId { get; set; }
                public BaseStatsProperty BaseStats { get; set; }
                public List<string> Types { get; set; }
                public List<string> FastMoves { get; set; }
                public List<string> ChargedMoves { get; set; }
                public List<string> LegacyMoves { get; set; }

                public class BaseStatsProperty
                {
                    public int Atk { get; set; }
                    public int Def { get; set; }
                    public int Hp { get; set; }
                }
            }

            public class MovesProperty
            {
                public string MoveId { get; set; }
                public string Name { get; set; }
                public string Type { get; set; }
                public int Power { get; set; }
                public int Energy { get; set; }
                public int EnergyGain { get; set; }
                public int? Cooldown { get; set; }
            }

            public string ToJson()
            {
                return JsonConvert.SerializeObject(this, Formatting.Indented, _jsonSerializerSettings);
            }
        }
    }
}