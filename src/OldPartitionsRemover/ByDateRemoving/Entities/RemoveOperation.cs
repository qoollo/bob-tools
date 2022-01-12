using System;
using System.Threading.Tasks;
using OldPartitionsRemover.Entites;

namespace OldPartitionsRemover.ByDateRemoving.Entities
{
    internal class RemoveOperation
    {
        public RemoveOperation(Func<Task<Result<bool>>> func)
        {
            Func = func;
        }

        public Func<Task<Result<bool>>> Func { get; }
    }
}