using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace AzureStorageAttackTest;

public static class Program
{
    private const string ConnectionString =
        "DefaultEndpointsProtocol=https;AccountName=blobtestacount;AccountKey=oFLFwmDKeSw36qs6qV6kywq2ffTK4vGA2xUb3dmf4vNmhR9k9GBsnA1uGEhqmczAdpKd5UNOrW7++ASt/wl4+A==;EndpointSuffix=core.windows.net";


    private const string ContainerName = "attacktest";
    private const string FileName = "attackfile";

    private static async Task Main()
    {
        var taskList = new List<Task<int>>();
        for (int i = 0; i < 200; i++)
        {
            var task = Inclement();
            taskList.Add(task);
        }

        await Task.WhenAll(taskList);

        var str = taskList.Aggregate("", (current, task) => current + (task.Result + " ,"));

        Console.WriteLine(str);
    }

    private static async Task<int> Inclement()
    {
        var container = new BlobContainerClient(ConnectionString, ContainerName);
        var blobClient = container.GetBlobClient(FileName);
        var leaseClient = blobClient.GetBlobLeaseClient();
        var leaseId = "";

        while (true)
        {
            try
            {
                var lease = await leaseClient.AcquireAsync(TimeSpan.FromSeconds(15));
                leaseId = lease.Value.LeaseId;
                Console.WriteLine("ロック権取得");
                break;
            }
            catch (Exception)
            {
                Console.WriteLine("アクセス権がないので0.１秒待ちます");
                //失敗したら0.1秒後再攻撃
                await Task.Delay(100);
            }
        }

        var nextValue = 0;
        try
        {
            var download = await blobClient.DownloadContentAsync(new BlobDownloadOptions()
            {
                Conditions = new BlobRequestConditions()
                {
                    LeaseId = leaseId
                }
            });

            nextValue = int.Parse(download.Value.Content.ToString());
            Console.WriteLine("ダウンロード成功" + nextValue);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        nextValue++;

        try
        {
            await blobClient.UploadAsync(new BinaryData(nextValue), new BlobUploadOptions()
            {
                Conditions = new BlobRequestConditions()
                {
                    LeaseId = leaseId
                }
            });

            Console.WriteLine("アップロード成功" + nextValue);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        await leaseClient.ReleaseAsync();

        return nextValue;
    }
}