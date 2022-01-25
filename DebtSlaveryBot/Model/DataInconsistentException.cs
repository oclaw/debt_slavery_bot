using System;

namespace DebtSlaveryBot.Model
{
    public class DataInconsistentException : Exception
    {
        public DataInconsistentException(string message) : base(message) { }
    }
}
