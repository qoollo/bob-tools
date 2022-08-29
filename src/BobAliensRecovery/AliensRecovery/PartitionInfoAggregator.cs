using System.Collections.Generic;
using System.IO;
using System.Linq;
using BobAliensRecovery.AliensRecovery.Entities;
using Microsoft.Extensions.Logging;

namespace BobAliensRecovery.AliensRecovery
{
    public class PartitionInfoAggregator
    {
        private static readonly char[] s_pathSeparators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        private readonly ILogger<PartitionInfoAggregator> _logger;

        public PartitionInfoAggregator(ILogger<PartitionInfoAggregator> logger)
        {
            _logger = logger;
        }

        internal IEnumerable<PartitionInfo> GetPartitionInfos(IEnumerable<string> syncedFiles)
        {
            var filesByPartitionAndVdiskid = new Dictionary<(int, string), List<string>>();
            foreach (var file in syncedFiles)
            {
                var components = file.Split(s_pathSeparators);
                if (components.Length >= 3 && int.TryParse(components[^3], out var vdiskId))
                {
                    var partition = components[^2];
                    if (filesByPartitionAndVdiskid.TryGetValue((vdiskId, partition), out var fileInfos))
                        fileInfos.Add(file);
                    else
                        filesByPartitionAndVdiskid.Add((vdiskId, partition), new List<string> { file });
                }
                else
                    _logger.LogWarning("Found file without whole info: {filename}", file);
            }

            var result = new List<PartitionInfo>();
            foreach (var ((vdiskId, partition), files) in filesByPartitionAndVdiskid)
            {
                var blobs = GetBlobs(files);
                result.Add(new PartitionInfo(vdiskId, partition, blobs));
            }
            return result;
        }

        private List<BlobInfo> GetBlobs(List<string> files)
        {
            var filesByBlob = new Dictionary<string, List<string>>();
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (filesByBlob.TryGetValue(name, out var fileInfos))
                    fileInfos.Add(file);
                else
                    filesByBlob.Add(name, new List<string> { file });
            }
            var blobs = new List<BlobInfo>();
            foreach (var (blobName, blobFiles) in filesByBlob)
            {
                var blobFile = blobFiles.FirstOrDefault(f => f.EndsWith(".blob"));
                var indexFile = blobFiles.FirstOrDefault(f => f.EndsWith(".index"));
                if (blobFile != null)
                    blobs.Add(new BlobInfo(blobFile, indexFile, blobFiles));
                else
                    _logger.LogWarning("Found blob without .blob file: {filename}", blobFiles.First());
            }

            return blobs;
        }
    }
}
