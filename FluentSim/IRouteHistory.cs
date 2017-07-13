using System.Collections.Generic;

namespace FluentSim
{
    public interface IRouteHistory
    {
        IReadOnlyList<ReceivedRequest> ReceivedRequests { get; }
    }
}