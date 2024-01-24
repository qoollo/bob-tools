using System.Collections.Generic;
using System.Linq;
using System.Net;
using RemoteFileCopy.Entities;

namespace ClusterModifier;

public class WorkSpecificationFinder
{
    public WorkSpecification Find(ClusterState clusterState)
    {
        var dirsToDelete = GetDirsToDelete(clusterState.VDiskInfo);
        var copyOperations = GetCopyOperations(clusterState.VDiskInfo, dirsToDelete);
        var confirmedDelete = GetConfirmedDeleteOperations(copyOperations, dirsToDelete);
        var unconfirmedDelete = GetUnconfirmedDeleteOperations(copyOperations, dirsToDelete);
        return new WorkSpecification(copyOperations, confirmedDelete, unconfirmedDelete);
    }

    private List<CopyOperation> GetCopyOperations(
        List<VDiskInfo> vDiskInfo,
        HashSet<RemoteDir> dirsToDelete
    )
    {
        var newNodeDisksByRemoteDir = GetNewNodeDisksByRemoteDir(vDiskInfo);
        var sourceDirsByDest = GetSourceDirsByDestination(vDiskInfo);
        return CollectOperations(sourceDirsByDest, newNodeDisksByRemoteDir, dirsToDelete);
    }

    private Dictionary<RemoteDir, List<NodeDisk>> GetNewNodeDisksByRemoteDir(
        List<VDiskInfo> vDiskInfo
    )
    {
        // Technically we can have multiple disks for single remote dir (multiple nodes or multiple disks in one dir)
        return vDiskInfo
            .SelectMany(i => i.NewDirs)
            .GroupBy(d => d.Dir)
            .ToDictionary(g => g.Key, g => g.Select(d => d.NodeDisk).ToList());
    }

    private Dictionary<RemoteDir, HashSet<RemoteDir>> GetSourceDirsByDestination(
        List<VDiskInfo> vDiskInfo
    )
    {
        var sourceDirsByDest = new Dictionary<RemoteDir, HashSet<RemoteDir>>();
        foreach (var info in vDiskInfo)
        {
            var newDirs = info.NewDirs.Select(d => d.Dir);
            var oldDirs = info.OldDirs.Select(d => d.Dir).ToHashSet();
            var missing = newDirs.Except(oldDirs);
            foreach (var newDir in missing)
                if (sourceDirsByDest.TryGetValue(newDir, out var dirs))
                    dirs.UnionWith(oldDirs);
                else
                    sourceDirsByDest.Add(newDir, oldDirs);
        }
        return sourceDirsByDest;
    }

    private static List<CopyOperation> CollectOperations(
        Dictionary<RemoteDir, HashSet<RemoteDir>> sourceDirsByDest,
        Dictionary<RemoteDir, List<NodeDisk>> newNodeDisksByRemoteDir,
        HashSet<RemoteDir> dirsToDelete
    )
    {
        var loadCountByAddress = new Dictionary<IPAddress, int>();
        var loadCountByDir = new Dictionary<RemoteDir, int>();
        foreach (var sources in sourceDirsByDest.Values)
        foreach (var src in sources)
        {
            loadCountByAddress[src.Address] = 0;
            loadCountByDir[src] = 0;
        }
        var operations = new List<CopyOperation>();
        foreach (
            var (dest, sources) in sourceDirsByDest
                .OrderBy(kv => kv.Key.Address.ToString())
                .ThenBy(kv => kv.Key.Path)
        )
        {
            var bestSource = sources
                .OrderByDescending(rd => dirsToDelete.Contains(rd) && loadCountByDir[rd] == 0)
                .ThenBy(rd => loadCountByAddress[rd.Address] - (rd.Address == dest.Address ? 1 : 0))
                .ThenBy(rd => rd.Address.ToString())
                .ThenBy(rd => rd.Path)
                .First();
            loadCountByAddress[bestSource.Address]++;
            loadCountByDir[bestSource]++;
            operations.Add(new CopyOperation(bestSource, dest, newNodeDisksByRemoteDir[dest]));
        }

        return operations;
    }

    private HashSet<RemoteDir> GetDirsToDelete(List<VDiskInfo> vDiskInfo)
    {
        var result = new HashSet<RemoteDir>();
        foreach (var info in vDiskInfo)
        {
            result.UnionWith(info.OldDirs.Except(info.NewDirs).Select(d => d.Dir));
        }
        return result;
    }

    private List<ConfirmedDeleteOperation> GetConfirmedDeleteOperations(
        List<CopyOperation> copyOperations,
        HashSet<RemoteDir> dirsToDelete
    )
    {
        var result = new List<ConfirmedDeleteOperation>();
        var copiedNewByOldDir = copyOperations
            .GroupBy(t => t.From)
            .ToDictionary(g => g.Key, g => g.Select(t => t.To).Distinct().ToArray());
        foreach (var oldDir in dirsToDelete)
        {
            if (copiedNewByOldDir.TryGetValue(oldDir, out var copiedNewDirs))
            {
                result.Add(new ConfirmedDeleteOperation(oldDir, copiedNewDirs));
            }
        }
        return result;
    }

    private List<UnconfirmedDeleteOperation> GetUnconfirmedDeleteOperations(
        List<CopyOperation> copyOperations,
        HashSet<RemoteDir> dirsToDelete
    )
    {
        var copied = copyOperations.Select(o => o.From).ToHashSet();
        return dirsToDelete.Except(copied).Select(d => new UnconfirmedDeleteOperation(d)).ToList();
    }
}

public record struct CopyOperation(
    RemoteDir From,
    RemoteDir To,
    List<NodeDisk> AffectedNewNodeDisks
);

public record struct ConfirmedDeleteOperation(RemoteDir DirToDelete, RemoteDir[] Copies);

public record struct UnconfirmedDeleteOperation(RemoteDir DirToDelete);

public record struct WorkSpecification(
    List<CopyOperation> CopyOperations,
    List<ConfirmedDeleteOperation> ConfirmedDeleteOperations,
    List<UnconfirmedDeleteOperation> UnconfirmedDeleteDirs
);
