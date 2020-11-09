using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using BobApi.Entities;
using Newtonsoft.Json;

namespace BobApi
{
    public class BobApiClient : IDisposable
    {
        private readonly HttpClient client;

        public BobApiClient(Uri address)
        {
            client = new HttpClient
            {
                BaseAddress = address,
                Timeout = TimeSpan.FromSeconds(5),
            };
        }

        public async Task<Node?> GetStatus()
        {
            try
            {
                var response = await client.GetAsync("status");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync()
                        .ContinueWith(t => JsonConvert.DeserializeObject<Node>(t.Result));
            }
            catch (HttpRequestException) { }
            return null;
        }

        public async Task<List<Node>> GetNodes()
        {
            try
            {
                var response = await client.GetAsync("nodes");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync()
                        .ContinueWith(t => JsonConvert.DeserializeObject<List<Node>>(t.Result));
            }
            catch (HttpRequestException) { }
            return null;
        }

        public async Task<List<Directory>> GetDirectories(VDisk vdisk)
        {
            var response = await client.GetAsync($"vdisks/{vdisk.Id}/replicas/local/dirs");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync()
                    .ContinueWith(t => JsonConvert.DeserializeObject<List<Directory>>(t.Result));
            return null;
        }

        public async Task<List<VDisk>> GetVDisks()
        {
            var response = await client.GetAsync("vdisks");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync()
                    .ContinueWith(t => JsonConvert.DeserializeObject<List<VDisk>>(t.Result));
            return null;
        }

        public async Task<List<string>> GetPartitions(VDisk vDisk)
        {
            var response = await client.GetAsync($"vdisks/{vDisk.Id}/partitions");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync()
                    .ContinueWith(t => JsonConvert.DeserializeAnonymousType(t.Result, new
                    {
                        Partitions = new
                            List<string>()
                    }).Partitions);
            }

            return null;
        }

        public async Task DeletePartition(VDisk vDisk, long? partition)
        {
            await client.DeleteAsync($"vdisks/{vDisk.Id}/partitions/{partition}");
        }

        public async Task<Partition?> GetPartition(VDisk vDisk, string partition)
        {
            var result = await client.GetAsync($"vdisks/{vDisk.Id}/partitions/{partition}");
            if (!result.IsSuccessStatusCode)
                return null;
            var content = await result.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Partition>(content);

        }

        public async Task<long?> CountRecordsOnVDisk(VDisk vDisk)
        {
            try
            {
                var result = await client.GetAsync($"vdisks/{vDisk.Id}/records/count");
                if (!result.IsSuccessStatusCode)
                    return null;
                return long.Parse(await result.Content.ReadAsStringAsync());
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            return null;
        }

        public void Dispose()
        {
            client?.Dispose();
        }
    }
}
