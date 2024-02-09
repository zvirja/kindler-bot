using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace KindlerBot.IO;

internal abstract class FileSystemStoreBase<TStoreData> where TStoreData: class, new()
{
    private readonly SemaphoreSlim _storeLock = new(1, 1);

    private string StoreFilePath { get; }

    protected FileSystemStoreBase(string storeFileName, IOptions<DeploymentConfiguration> deploymentConfig)
    {
        StoreFilePath = string.IsNullOrEmpty(deploymentConfig.Value.ConfigStore)
            ? Path.Join(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location), storeFileName)
            : Path.Join(deploymentConfig.Value.ConfigStore, storeFileName);
    }

    protected async Task<TStoreData> GetStoreData()
    {
        if (!File.Exists(StoreFilePath))
            return new TStoreData();

        var fileContents = await File.ReadAllTextAsync(StoreFilePath);
        return JsonConvert.DeserializeObject<TStoreData>(fileContents)!;
    }

    protected async Task UpdateStoreData(Action<TStoreData> updateData)
    {
        await _storeLock.WaitAsync();
        try
        {
            var storeData = await GetStoreData();
            updateData(storeData);
            await File.WriteAllTextAsync(StoreFilePath, JsonConvert.SerializeObject(storeData, Formatting.Indented));
        }
        finally
        {
            _storeLock.Release();
        }
    }
}
