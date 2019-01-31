using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PvPoke.FileManagement
{
    public static class FileManager
    {
        private static readonly HttpClient _client = new HttpClient();

        public static bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }

        public static async Task<string> FetchFileAsync(string remoteFileUri)
        {
            Console.WriteLine("Fetching file...");
            return await _client.GetStringAsync(remoteFileUri);
        }

        public static async Task SaveFileAsync(string json, string localFilePath)
        {
            Console.WriteLine("Writing file...");
            new FileInfo(localFilePath).Directory?.Create(); // create folder if it doesn't already exist
            await File.WriteAllTextAsync(localFilePath, JToken.Parse(json).ToString(Formatting.Indented));
        }

        public static async Task<string> ReadFileAsync(string filePath)
        {
            Console.WriteLine("Reading file...");
            return await File.ReadAllTextAsync(filePath);
        }

        public static T LoadFile<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}