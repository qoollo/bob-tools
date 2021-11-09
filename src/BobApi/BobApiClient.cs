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

        public async Task<BobApiResult<Node>> GetStatus(CancellationToken cancellationToken = default)
            => await GetJson<Node>("status", cancellationToken: cancellationToken);

        public async Task<BobApiResult<List<Node>>> GetNodes(CancellationToken cancellationToken = default)
            => await GetJson<List<Node>>("nodes", cancellationToken);

        public async Task<BobApiResult<List<Disk>>> GetDisks(CancellationToken cancellationToken = default)
            => await GetJson<List<Disk>>("disks/list", cancellationToken);

        public async Task<BobApiResult<List<Disk>>> GetInactiveDisks(CancellationToken cancellationToken = default)
        {
            var disksResult = await GetDisks(cancellationToken);
            if (disksResult.TryGetData(out var disks))
            {
                if (disks != null)
                    disks.RemoveAll(d => d.IsActive);
                return BobApiResult<List<Disk>>.Ok(disks);
            }
            return disksResult;
        }

        public async Task<BobApiResult<bool>> StartDisk(string name, CancellationToken cancellationToken = default)
            => await PostIsOk($"disks/{name}/start", cancellationToken);

        public async Task<BobApiResult<bool>> StopDisk(string name, CancellationToken cancellationToken = default)
            => await PostIsOk($"disks/{name}/stop", cancellationToken);

        public async Task<BobApiResult<bool>> RestartDisk(string diskName, CancellationToken cancellationToken = default)
        {
            var stopResult = await StopDisk(diskName, cancellationToken);
            if (stopResult.TryGetData(out var r) && r)
                return await StartDisk(diskName, cancellationToken);
            return stopResult;
        }

        public async Task<BobApiResult<List<Directory>>> GetDirectories(VDisk vdisk, CancellationToken cancellationToken = default)
            => await GetJson<List<Directory>>($"vdisks/{vdisk.Id}/replicas/local/dirs", cancellationToken);

        public async Task<BobApiResult<Directory>> GetAlienDirectory(CancellationToken cancellationToken = default)
            => await GetJson<Directory>("alien/dir", cancellationToken);

        public async Task<BobApiResult<bool>> SyncAlienData(CancellationToken cancellationToken = default)
            => await PostIsOk("alien/detach", cancellationToken);

        public async Task<BobApiResult<List<VDisk>>> GetVDisks(CancellationToken cancellationToken = default)
            => await GetJson<List<VDisk>>("vdisks", cancellationToken);

        public async Task<BobApiResult<List<string>>> GetPartitions(VDisk vDisk)
        {
            return await InvokeRequest<List<string>>(async client =>
            {
                using (var response = await client.GetAsync($"vdisks/{vDisk.Id}/partitions"))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        return BobApiResult<List<string>>.Ok(await response.Content.ReadAsStringAsync()
                            .ContinueWith(t => JsonConvert.DeserializeAnonymousType(t.Result, new
                            {
                                Partitions = new List<string>()
                            }).Partitions));
                    }
                    return BobApiResult<List<string>>.Unsuccessful();
                }
            });
        }

        public async Task DeletePartition(VDisk vDisk, long? partition)
            => await _client.DeleteAsync($"vdisks/{vDisk.Id}/partitions/{partition}");

        public async Task<BobApiResult<Partition>> GetPartition(VDisk vDisk, string partition,
            CancellationToken cancellationToken = default)
            => await GetJson<Partition>($"vdisks/{vDisk.Id}/partitions/{partition}", cancellationToken: cancellationToken);


        public async Task<BobApiResult<long>> CountRecordsOnVDisk(VDisk vDisk, CancellationToken cancellationToken = default)
            => await GetJson<long>($"vdisks/{vDisk.Id}/records/count", cancellationToken: cancellationToken);

        public async Task<BobApiResult<NodeConfiguration>> GetNodeConfiguration(CancellationToken cancellationToken = default)
            => await GetJson<NodeConfiguration>("configuration", cancellationToken: cancellationToken);

        public void Dispose()
        {
            _client?.Dispose();
        }

        public override string ToString()
        {
            return _client.BaseAddress.ToString();
        }

        private async Task<BobApiResult<T>> GetJson<T>(string addr, CancellationToken cancellationToken = default)
            => await Get(addr, JsonConvert.DeserializeObject<T>, cancellationToken);


        private async Task<BobApiResult<bool>> PostIsOk(string addr, CancellationToken cancellationToken = default)
        {
            return await InvokeRequest(async client =>
            {
                using (var response = await client.PostAsync(addr, new StringContent(""), cancellationToken: cancellationToken))
                    return BobApiResult<bool>.Ok(response.IsSuccessStatusCode);
            });
        }

        private async Task<BobApiResult<T>> Get<T>(string addr, Func<string, T> parse,
            CancellationToken cancellationToken = default)
        {
            return await InvokeRequest(async client =>
            {
                using (var response = await client.GetAsync(addr, cancellationToken))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        return BobApiResult<T>.Ok(parse(content));
                    }
                    return BobApiResult<T>.Unsuccessful();
                }
            });
        }

        private async Task<BobApiResult<T>> InvokeRequest<T>(Func<HttpClient, Task<BobApiResult<T>>> f)
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
                return BobApiResult<T>.Unavailable();
            }
        }
    }
}
