namespace RecordsCalculator.Entities
{
    public readonly struct RecordsCount
    {
        public RecordsCount(long unique, long withReplicas)
        {
            Unique = unique;
            WithReplicas = withReplicas;
        }

        public long Unique { get; }
        public long WithReplicas { get; }
    }
}