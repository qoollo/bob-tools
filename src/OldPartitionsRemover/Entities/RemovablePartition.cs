using System;
using System.Threading;
using System.Threading.Tasks;

namespace OldPartitionsRemover.Entities;

public record class RemovablePartition(
    string Id,
    DateTimeOffset Timestamp,
    Func<CancellationToken, Task> Remove
);
