using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BobApi.Entities;
using Newtonsoft.Json;
using Path = System.IO.Path;

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
                Timeout = TimeSpan.FromSeconds(30),
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

        public async Task<List<Disk>> GetDisks()
        {
            try
            {
                using (var response = await client.GetAsync("disks/list"))
                {
                    if (response.IsSuccessStatusCode)
                        return await response.Content.ReadAsStringAsync().ContinueWith(t => JsonConvert.DeserializeObject<List<Disk>>(t.Result));
                }
            }
            catch (HttpRequestException) { }
            return null;
        }

        public async Task<List<Disk>> GetInactiveDisks()
        {
            var disks = await GetDisks();
            if (disks != null)
                disks.RemoveAll(d => d.IsActive);
            return disks;
        }

        public async Task<bool> StartDisk(string name, CancellationToken cancellationToken = default)
        {
            try
            {
                using (var response = await client.PostAsync($"disks/{name}/start", new StringContent(""), cancellationToken))
                    return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> StopDisk(string name, CancellationToken cancellationToken = default)
        {
            try
            {
                using (var response = await client.PostAsync($"disks/{name}/stop", new StringContent(""), cancellationToken))
                    return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RestartDisk(string diskName, CancellationToken cancellationToken = default)
        {
            return await StopDisk(diskName, cancellationToken) && await StartDisk(diskName, cancellationToken);
        }

        public async Task<List<Directory>> GetDirectories(VDisk vdisk, CancellationToken cancellationToken = default)
        {
            var response = await client.GetAsync($"vdisks/{vdisk.Id}/replicas/local/dirs", cancellationToken: cancellationToken);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync()
                    .ContinueWith(t => JsonConvert.DeserializeObject<List<Directory>>(t.Result));
            return null;
        }

        public async Task<Directory> GetAlienDirectory()
        {
            try
            {
                var response = await client.GetAsync("alien/dir");
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<Directory>(content);
            }
            catch (HttpRequestException)
            {
                return default;
            }
        }

        public async Task<bool> SyncAlienData(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await client.PostAsync("alien/sync", new StringContent(""), cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }

        public async Task<List<VDisk>> GetVDisks(CancellationToken cancellationToken = default)
        {
            var response = await client.GetAsync("vdisks", cancellationToken: cancellationToken);
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
                        Partitions = new List<string>()
                    }).Partitions);
            }

            return null;
        }

        public async Task<string> GetAlienDiskName()
        {
            const string SpecialDiskName = "alien_disk";
            var disks = await GetDisks();
            if (disks is null)
                return null;
            if (disks.Any(d => d.Name == SpecialDiskName))
                return SpecialDiskName;

            var byName = disks.GroupBy(d => d.Name);
            var multinameGroup = byName.FirstOrDefault(g => g.Count() > 1);
            if (multinameGroup != null)
                return multinameGroup.Key;

            return disks.OrderBy(d => d.Name).FirstOrDefault().Name;
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

        public override string ToString()
        {
            return client.BaseAddress.ToString();
        }
    }
}
