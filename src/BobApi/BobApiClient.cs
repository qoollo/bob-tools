using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BobApi.BobEntities;
using BobApi.Entities;
using BobApi.Exceptions;
using Newtonsoft.Json;
using Path = System.IO.Path;

namespace BobApi
{
    public class BobApiClient : IDisposable
    {
        private readonly HttpClient _client;
        private readonly bool _throwOnNoConnection;

        public BobApiClient(Uri address, bool throwOnNoConnection = false)
        {
            _client = new HttpClient
            {
                BaseAddress = address,
                Timeout = TimeSpan.FromSeconds(30),
            };
            _throwOnNoConnection = throwOnNoConnection;
        }

        public async Task<Node?> GetStatus(CancellationToken cancellationToken = default)
            => await GetJson<Node?>("status", cancellationToken: cancellationToken);

        public async Task<List<Node>> GetNodes(CancellationToken cancellationToken = default)
            => await GetJson<List<Node>>("nodes", () => throw new HttpRequestException(), cancellationToken);

        public async Task<List<Disk>> GetDisks(CancellationToken cancellationToken = default)
            => await GetJson<List<Disk>>("disks/list", () => throw new HttpRequestException(), cancellationToken);

        public async Task<List<Disk>> GetInactiveDisks(CancellationToken cancellationToken = default)
        {
            var disks = await GetDisks(cancellationToken);
            if (disks != null)
                disks.RemoveAll(d => d.IsActive);
            return disks;
        }

        public async Task<bool> StartDisk(string name, CancellationToken cancellationToken = default)
            => await PostIsOk($"disks/{name}/start", cancellationToken);

        public async Task<bool> StopDisk(string name, CancellationToken cancellationToken = default)
            => await PostIsOk($"disks/{name}/stop", cancellationToken);

        public async Task<bool> RestartDisk(string diskName, CancellationToken cancellationToken = default)
            => await StopDisk(diskName, cancellationToken) && await StartDisk(diskName, cancellationToken);

        public async Task<List<Directory>> GetDirectories(VDisk vdisk, CancellationToken cancellationToken = default)
            => await GetJson<List<Directory>>($"vdisks/{vdisk.Id}/replicas/local/dirs", () => throw new HttpRequestException(),
                cancellationToken: cancellationToken);

        public async Task<Directory> GetAlienDirectory(CancellationToken cancellationToken = default)
            => await GetJson<Directory>("alien/dir", cancellationToken: cancellationToken);

        public async Task<bool> SyncAlienData(CancellationToken cancellationToken = default)
            => await PostIsOk("alien/sync", cancellationToken);

        public async Task<List<VDisk>> GetVDisks(CancellationToken cancellationToken = default)
            => await GetJson("vdisks", () => new List<VDisk>(), cancellationToken);

        public async Task<List<string>> GetPartitions(VDisk vDisk)
        {
            var response = await _client.GetAsync($"vdisks/{vDisk.Id}/partitions");
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

        public async Task DeletePartition(VDisk vDisk, long? partition)
            => await _client.DeleteAsync($"vdisks/{vDisk.Id}/partitions/{partition}");

        public async Task<Partition?> GetPartition(VDisk vDisk, string partition,
            CancellationToken cancellationToken = default)
            => await GetJson<Partition>($"vdisks/{vDisk.Id}/partitions/{partition}", cancellationToken: cancellationToken);


        public async Task<long?> CountRecordsOnVDisk(VDisk vDisk, CancellationToken cancellationToken = default)
            => await GetJson<long?>($"vdisks/{vDisk.Id}/records/count", cancellationToken: cancellationToken);

        public async Task<NodeConfiguration> GetNodeConfiguration(CancellationToken cancellationToken = default)
            => await GetJson<NodeConfiguration>("configuration", cancellationToken: cancellationToken);

        public void Dispose()
        {
            _client?.Dispose();
        }

        public override string ToString()
        {
            return _client.BaseAddress.ToString();
        }

        private async Task<T> GetJson<T>(string addr,
            Func<T> defValueCreator = null, CancellationToken cancellationToken = default)
            => await Get(addr, JsonConvert.DeserializeObject<T>, defValueCreator, cancellationToken);


        private async Task<bool> PostIsOk(string addr, CancellationToken cancellationToken = default)
        {
            return await InvokeRequest(async client =>
            {
                using (var response = await client.PostAsync(addr, new StringContent(""), cancellationToken: cancellationToken))
                    return response.IsSuccessStatusCode;
            });
        }

        private async Task<T> Get<T>(string addr, Func<string, T> parse,
            Func<T> defValueCreator = null,
            CancellationToken cancellationToken = default)
        {
            return await InvokeRequest(async client =>
            {
                using (var response = await client.GetAsync(addr, cancellationToken))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        return parse(content);
                    }
                    return defValueCreator is null ? default : defValueCreator();
                }
            });
        }

        private async Task<T> InvokeRequest<T>(Func<HttpClient, Task<T>> f)
        {
            try
            {
                return await f(_client);
            }
            // Since .net 5 TaskCanceledException is thrown on timeout
            catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
            {
                if (_throwOnNoConnection)
                    throw new BobConnectionException(_client.BaseAddress, e);
            }

            return default;
        }
    }
}
