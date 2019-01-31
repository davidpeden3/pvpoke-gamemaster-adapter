using System;
using System.Threading.Tasks;
using PvPoke.FileManagement.PokemonGo;

namespace PvPoke
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            PokemonGoGameMasterFileManager.GameMasterFile gameMasterFile;

            if (!PokemonGoGameMasterFileManager.FileExists())
            {
                gameMasterFile = await PokemonGoGameMasterFileManager.FetchAndSaveFileAsync();
            }
            else
            {
                long latestGameMasterVersion = await PokemonGoGameMasterFileManager.FetchLatestVersionAsync();

                string json = await PokemonGoGameMasterFileManager.ReadFileAsync();
                gameMasterFile = PokemonGoGameMasterFileManager.LoadFile(json);

                if (gameMasterFile.TimeStampMs != latestGameMasterVersion)
                {
                    gameMasterFile = await PokemonGoGameMasterFileManager.FetchAndSaveFileAsync();
                }
            }

            Console.WriteLine(gameMasterFile.TimeStampMs);
        }
    }
}