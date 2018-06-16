using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;

namespace FluentSim
{
    public class FluentConfigurator : RouteConfigurer
    {
        private string Output = "";
        private string Path;
        private HttpVerb HttpVerb;
        private ManualResetEventSlim RespondToRequests = new ManualResetEventSlim(true);
        private TimeSpan RouteDelay;
        private List<Action<HttpListenerContext>> ResponseModifiers = new List<Action<HttpListenerContext>>();
        private JsonSerializerSettings JsonSerializerSettings;
        private List<ReceivedRequest> ReceivedRequests = new List<ReceivedRequest>();

        public FluentConfigurator(string path, HttpVerb get, JsonSerializerSettings jsonConverter)
        {
            JsonSerializerSettings = jsonConverter;
            HttpVerb = get;
            Path = path;
        }

        public HttpMethod Method { get; set; }
        public bool IsRegex { get; set; }

        public void AddReceivedRequest(ReceivedRequest request)
        {
            ReceivedRequests.Add(request);
        }

        public RouteConfigurer Responds<T>(T output)
        {
            Output = JsonConvert.SerializeObject(output, JsonSerializerSettings);
            return this;
        }

        public RouteConfigurer WithCode(int code)
        {
            ResponseModifiers.Add(ctx => ctx.Response.StatusCode = code);
            return this;
        }

        public RouteConfigurer WithHeader(string headerName, string headerValue)
        {
            ResponseModifiers.Add(ctx => ctx.Response.AddHeader(headerName, headerValue));
            return this;
        }

        public RouteConfigurer Delay(TimeSpan routeDelay)
        {
            RouteDelay = routeDelay;
            return this;
        }

        public RouteConfigurer Pause()
        {
            RespondToRequests.Reset();
            return this;
        }

        public RouteConfigurer Resume()
        {
            RespondToRequests.Set();
            return this;
        }

        public RouteConfigurer Responds(string output)
        {
            Output = output;
            return this;
        }

        public RouteConfigurer MatchingRegex()
        {
            IsRegex = true;
            return this;
        }

        internal string GetBody()
        {
            return Output;
        }

        internal bool DoesRouteMatch(HttpListenerRequest contextRequest)
        {
            if (!Path.EndsWith("/") && !IsRegex) Path += "/";
            var requestPath = contextRequest.Url.LocalPath;
            if (!requestPath.EndsWith("/")) requestPath += "/";

            var pathMatches = DoesPathMatch(requestPath);
            var verbMatches = HttpVerb.ToString().ToUpper() == contextRequest.HttpMethod;
            return pathMatches && verbMatches;
        }

        private bool DoesPathMatch(string requestPath)
        {
            if (IsRegex)
            {
                return Regex.Match(requestPath.ToLower(), Path.ToLower()).Success;
            }

            return string.Equals(requestPath, Path, StringComparison.CurrentCultureIgnoreCase);
        }

        internal void WaitUntilReadyToRespond()
        {
            Thread.Sleep(RouteDelay);
            RespondToRequests.Wait();
        }

        internal void RunContextModifiers(HttpListenerContext context)
        {
            foreach (var responseModifier in ResponseModifiers)
                responseModifier(context);
        }

        public RouteConfigurer Responds()
        {
            return this;
        }

        public RouteConfigurer WithCookie(Cookie cookie)
        {
            ResponseModifiers.Add(ctx => ctx.Response.SetCookie(cookie));
            return this;
        }

        public IRouteHistory History()
        {
            return new RouteHistory(ReceivedRequests.AsReadOnly());
        }

        private class RouteHistory : IRouteHistory
        {
            public RouteHistory(IReadOnlyList<ReceivedRequest> requests)
            {
                ReceivedRequests = requests;
            }
            public IReadOnlyList<ReceivedRequest> ReceivedRequests { get; }
        }
    }

}