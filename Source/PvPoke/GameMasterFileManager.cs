using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PvPoke
{
    public static class GameMasterFileManager
    {
        private static readonly string _gameMasterVersionUri = "https://raw.githubusercontent.com/pokemongo-dev-contrib/pokemongo-game-master/master/versions/latest-version.txt";
        private static readonly string _gameMasterJsonUri = "https://raw.githubusercontent.com/pokemongo-dev-contrib/pokemongo-game-master/master/versions/latest/GAME_MASTER.json";
        private static readonly HttpClient _client = new HttpClient();

        private static string GameMasterJsonPath => Path.Combine(AppContext.BaseDirectory, "Data", "gameMaster.json");

        public static bool FileExists()
        {
            return File.Exists(GameMasterJsonPath);
        }

        public static async Task<long> FetchLatestVersionAsync()
        {
            Console.WriteLine("Fetching latest version #...");
            var currentGameMasterVersion = Convert.ToInt64(await _client.GetStringAsync(_gameMasterVersionUri));
            return currentGameMasterVersion;
        }

        public static async Task<GameMasterFile> OpenFileAsync()
        {
            Console.WriteLine("Reading file...");
            string json = await File.ReadAllTextAsync(GameMasterJsonPath);

            return JsonConvert.DeserializeObject<GameMasterFile>(json);
        }

        public static async Task<GameMasterFile> FetchAndSaveFileAsync()
        {
            Console.WriteLine("Fetching file...");
            string json = await _client.GetStringAsync(_gameMasterJsonUri);

            Console.WriteLine("Writing file...");
            await File.WriteAllTextAsync(GameMasterJsonPath, json);

            return JsonConvert.DeserializeObject<GameMasterFile>(json);
        }
    }
}