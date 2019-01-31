using System;
using System.IO;
using System.Threading.Tasks;

namespace PvPoke.FileManagement.PokemonGo
{
    public static class PokemonGoGameMasterFileManager
    {
        private static readonly string _gameMasterVersionUri = "https://raw.githubusercontent.com/pokemongo-dev-contrib/pokemongo-game-master/master/versions/latest-version.txt";
        private static readonly string _gameMasterJsonUri = "https://raw.githubusercontent.com/pokemongo-dev-contrib/pokemongo-game-master/master/versions/latest/GAME_MASTER.json";

        private static string GameMasterJsonPath => Path.Combine(AppContext.BaseDirectory, "Data", "gameMaster.json");

        public static bool FileExists()
        {
            return FileManager.FileExists(GameMasterJsonPath);
        }

        public static async Task<long> FetchLatestVersionAsync()
        {
            Console.WriteLine("Fetching latest version #...");
            return Convert.ToInt64(await FileManager.FetchFileAsync(_gameMasterVersionUri));
        }

        public static async Task<GameMasterFile> OpenFileAsync()
        {
            var json = await FileManager.ReadFileAsync(GameMasterJsonPath);
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
            public long TimeStampMs { get; set; }
        }
    }
}