using System.Collections.Generic;
using System.IO;
using System.Linq;
using BobAliensRecovery.AliensRecovery.Entities;
using Microsoft.Extensions.Logging;
using RemoteFileCopy.Rsync.Entities;

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

        internal IEnumerable<PartitionInfo> GetPartitionInfos(IEnumerable<RsyncFileInfo> syncedFiles)
        {
            var filesByPartitionAndVdiskid = new Dictionary<(int, string), List<RsyncFileInfo>>();
            foreach (var file in syncedFiles)
            {
                var components = file.Filename.Split(s_pathSeparators);
                if (components.Length >= 3 && int.TryParse(components[^3], out var vdiskId))
                {
                    var partition = components[^2];
                    if (filesByPartitionAndVdiskid.TryGetValue((vdiskId, partition), out var fileInfos))
                        fileInfos.Add(file);
                    else
                        filesByPartitionAndVdiskid.Add((vdiskId, partition), new List<RsyncFileInfo> { file });
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

        private List<BlobInfo> GetBlobs(List<RsyncFileInfo> files)
        {
            var filesByBlob = new Dictionary<string, List<RsyncFileInfo>>();
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file.Filename);
                if (filesByBlob.TryGetValue(name, out var fileInfos))
                    fileInfos.Add(file);
                else
                    filesByBlob.Add(name, new List<RsyncFileInfo> { file });
            }
            var blobs = new List<BlobInfo>();
            foreach (var (blobName, blobFiles) in filesByBlob)
            {
                var blobFile = blobFiles.FirstOrDefault(f => f.Filename.EndsWith(".blob"));
                var indexFile = blobFiles.FirstOrDefault(f => f.Filename.EndsWith(".index"));
                if (blobFile != null)
                    blobs.Add(new BlobInfo(blobFile, indexFile, blobFiles));
                else
                    _logger.LogWarning("Found blob without .blob file: {filename}", blobFiles.First().Filename);
            }

            return blobs;
        }
    }
}