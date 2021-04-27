namespace DisksMonitoring.OS.DisksFinding.LshwParsing
{
    public enum TokenType
    {
        Header,
        LogicalName,
        PhysicalId,
        GUID,
        Serial,
        LastMountPoint,
        State,
        MountFsType,
        Filesystem,
        MountOptions,
        Product
    }
}
