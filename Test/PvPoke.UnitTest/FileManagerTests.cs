using System.Threading.Tasks;
using PvPoke.FileManagement.PokemonGo;
using PvPoke.FileManagement.PvPoke;
using Xunit;
using Xunit.Abstractions;

namespace PvPoke.UnitTest
{
    public class FileManagerTests
    {
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
            var file = PokemonGoGameMasterFileManager.LoadFile(json);
            string serializedFile = file.ToJson();
            _output.WriteLine(serializedFile);
            Assert.Equal(json, serializedFile);
        }
    }
}