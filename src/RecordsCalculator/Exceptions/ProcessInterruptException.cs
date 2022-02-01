using System;

namespace RecordsCalculator
{
    public class ProcessInterruptException : Exception
    {
        public ProcessInterruptException(string message) : base(message)
        {
        }
    }
}