using System;
using System.Threading.Tasks;

namespace OldPartitionsRemover.Entities;

public record class RemovablePartition(string Id, DateTimeOffset Timestamp, Func<Task> Remove);
