namespace DisksMonitoring.OS.DisksFinding.LshwParsing
{
    public enum NodeType
    {
        Unknown,
        Volume,
        LogicalVolume,
        Disk,
        RAID,
        SCSI,
        IDE,
        SATA,
    }
}
