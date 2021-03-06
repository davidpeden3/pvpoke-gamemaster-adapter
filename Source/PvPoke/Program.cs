﻿using System;
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

                gameMasterFile = await PokemonGoGameMasterFileManager.LoadFileAsync(PokemonGoGameMasterFileManager.GameMasterJsonPath);

                if (gameMasterFile.TimeStampMs != latestGameMasterVersion)
                {
                    gameMasterFile = await PokemonGoGameMasterFileManager.FetchAndSaveFileAsync();
                }
            }

            Console.WriteLine(gameMasterFile.TimeStampMs);
        }
    }
}