using System;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using Octokit;

namespace Tgstation.Server.ApiUpdater
{
	static class Program
	{
		static async Task<byte[]> GetLatestApiBytes(IGitHubClient gitHubClient)
		{
			Console.WriteLine("Getting TGS releases...");
			var releases = await gitHubClient.Repository.Release.GetAll("tgstation", "tgstation-server");

			var latestApiRelease = releases.First(x => x.TagName.StartsWith("api", StringComparison.Ordinal));
			Console.WriteLine($"Latest API release: {latestApiRelease.TagName}");

			var apiAsset = latestApiRelease.Assets.First(x => x.Name.Equals("swagger.json", StringComparison.Ordinal));
			Console.WriteLine("Downloading json...");

			using var webClient = new HttpClient();
			return await webClient.GetByteArrayAsync(new Uri(apiAsset.BrowserDownloadUrl));
		}

		static async Task Main(string[] args)
		{
			var gitHubToken = args[0];
			var repoPath = args[1];

			var gitHubClient = new GitHubClient(new ProductHeaderValue("Tgstation.Server.ApiUpdater", "1.0.0"))
			{
				Credentials = new Credentials(gitHubToken)
			};

			var releaseTask = GetLatestApiBytes(gitHubClient);

			Console.WriteLine("Deleting old API...");

			var specPath = Path.Combine(repoPath, "swagger.yaml");
			if (File.Exists(specPath))
				File.Delete(specPath);

			var newSpecBytes = await releaseTask;

			Console.WriteLine("Converting new API to yaml...");

			var newSpecJson = Encoding.UTF8.GetString(newSpecBytes);
			var expConverter = new ExpandoObjectConverter();
			dynamic deserializedObject = JsonConvert.DeserializeObject<ExpandoObject>(newSpecJson, expConverter);

			var serializer = new YamlDotNet.Serialization.Serializer();
			string yaml = serializer.Serialize(deserializedObject);

			Console.WriteLine("Writing replacement API...");
			await File.WriteAllTextAsync(specPath, yaml);
		}
	}
}
