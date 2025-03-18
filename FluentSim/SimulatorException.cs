using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FluentSim
{
    public class SimulatorException : ApplicationException
    {
        public SimulatorException(string message = "An unexpected exception was thrown while processing the request. See the exception data for the exceptions thrown.") : base(message)
        {
            
        }

        public SimulatorException(List<Exception> listenerExceptions)
        {
            Data.Add("Exceptions", listenerExceptions);
        }
    }
}