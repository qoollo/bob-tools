using System;
using System.Threading.Tasks;
using OldPartitionsRemover.Entities;

namespace OldPartitionsRemover.ByDateRemoving.Entities
{
    internal delegate Task<Result<int>> RemoveOperation();
}