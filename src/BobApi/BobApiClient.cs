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

    public class BobApiClient : IDisposable, IPartitionsBobApiClient
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

        public async Task<BobApiResult<List<string>>> GetPartitions(VDisk vDisk, CancellationToken cancellationToken = default)
            => await GetPartitions(vDisk.Id, cancellationToken);

        public async Task<BobApiResult<List<string>>> GetPartitions(ClusterConfiguration.VDisk vDisk, CancellationToken cancellationToken = default)
            => await GetPartitions(vDisk.Id, cancellationToken);

        public async Task<BobApiResult<bool>> DeletePartitionsByTimestamp(long vDiskId, long timestamp, CancellationToken cancellationToken = default)
            => await DeleteIsOk($"vdisks/{vDiskId}/partitions/by_timestamp/{timestamp}", cancellationToken);

        public async Task<BobApiResult<Partition>> GetPartition(long id, string partition,
            CancellationToken cancellationToken = default)
            => await GetJson<Partition>($"vdisks/{id}/partitions/{partition}", cancellationToken: cancellationToken);


        public async Task<BobApiResult<long>> CountRecordsOnVDisk(long id, CancellationToken cancellationToken = default)
            => await GetJson<long>($"vdisks/{id}/records/count", cancellationToken: cancellationToken);

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

        private async Task<BobApiResult<List<string>>> GetPartitions(long id, CancellationToken cancellationToken)
        {
            return await InvokeRequest<List<string>>(async client =>
            {
                using (var response = await client.GetAsync($"vdisks/{id}/partitions", cancellationToken))
                {
                    return await ParseResponse(response, async resp =>
                    {
                        var content = await resp.Content.ReadAsStringAsync();
                        var partitions = JsonConvert.DeserializeAnonymousType(content, new
                        {
                            Partitions = new List<string>()
                        }).Partitions;
                        return partitions;
                    });
                }
            });
        }

        private async Task<BobApiResult<T>> GetJson<T>(string addr, CancellationToken cancellationToken = default)
            => await Get(addr, JsonConvert.DeserializeObject<T>, cancellationToken);


        private async Task<BobApiResult<bool>> PostIsOk(string addr, CancellationToken cancellationToken = default)
        {
            return await InvokeRequest(async client =>
            {
                using (var response = await client.PostAsync(addr, new StringContent(""), cancellationToken: cancellationToken))
                {
                    return await ParseResponse(response, _ => true);
                }
            });
        }

        private async Task<BobApiResult<bool>> DeleteIsOk(string addr, CancellationToken cancellationToken = default)
        {
            return await InvokeRequest(async client =>
            {
                using (var response = await client.DeleteAsync(addr, cancellationToken: cancellationToken))
                {
                    return await ParseResponse(response, _ => true);
                }
            });
        }

        private async Task<BobApiResult<T>> Get<T>(string addr, Func<string, T> parse,
            CancellationToken cancellationToken = default)
        {
            return await InvokeRequest(async client =>
            {
                using (var response = await client.GetAsync(addr, cancellationToken))
                {
                    return await ParseResponse(response, async resp => parse(await resp.Content.ReadAsStringAsync()));
                }
            });
        }

        private async Task<BobApiResult<T>> ParseResponse<T>(HttpResponseMessage response,
            Func<HttpResponseMessage, Task<T>> parse)
        {
            if (response.IsSuccessStatusCode)
                return BobApiResult<T>.Ok(await parse(response));
            return await BobApiResult<T>.Unsuccessful(response);
        }

        private async Task<BobApiResult<T>> ParseResponse<T>(HttpResponseMessage response, Func<HttpResponseMessage, T> parse)
        {
            if (response.IsSuccessStatusCode)
                return BobApiResult<T>.Ok(parse(response));
            return await BobApiResult<T>.Unsuccessful(response);
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
