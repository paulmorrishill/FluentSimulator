using System;
using System.Net;

namespace FluentSim
{
    public interface RouteConfigurer
    {
        RouteConfigurer Responds<T>(T output);
        RouteConfigurer Responds(byte[] output);
        RouteConfigurer Responds();
        RouteConfigurer Responds(string output);
        RouteConfigurer MatchingRegex();
        RouteConfigurer WithCode(int code);
        RouteConfigurer WithHeader(string headerName, string headerValue);
        RouteConfigurer Delay(TimeSpan routeDelay);
        RouteConfigurer Pause();
        RouteConfigurer Resume();
        RouteConfigurer WithCookie(Cookie cookie);
        IRouteHistory History();
    }
}