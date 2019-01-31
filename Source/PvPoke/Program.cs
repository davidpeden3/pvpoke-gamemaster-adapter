using System;
using System.Threading.Tasks;

namespace PvPoke
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            GameMasterFile gameMasterFile;

            if (!GameMasterFileManager.FileExists())
            {
                gameMasterFile = await GameMasterFileManager.FetchAndSaveFileAsync();
            }
            else
            {
                long latestGameMasterVersion = await GameMasterFileManager.FetchLatestVersionAsync();

                gameMasterFile = await GameMasterFileManager.OpenFileAsync();

                if (gameMasterFile.TimeStampMs != latestGameMasterVersion)
                {
                    gameMasterFile = await GameMasterFileManager.FetchAndSaveFileAsync();
                }
            }

            Console.WriteLine(gameMasterFile.TimeStampMs);
        }
    }

    public class GameMasterFile
    {
        public long TimeStampMs { get; set; }
    }
}