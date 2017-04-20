using System;
using System.Collections.Generic;

namespace FluentSim
{
    public class SimulatorException : ApplicationException
    {
        public SimulatorException() : base("An unexpected exception was thrown while processing the request. See the exception data for the exceptions thrown.")
        {
            
        }

        public SimulatorException(List<Exception> listenerExceptions)
        {
            Data.Add("Exceptions", listenerExceptions);
        }
    }
}