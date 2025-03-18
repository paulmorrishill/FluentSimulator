using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;

namespace FluentSim
{
    public class FluentConfigurator : RouteConfigurer, RouteSequenceConfigurer
    {
        private string Path;
        private HttpVerb HttpVerb;
        private ManualResetEventSlim RespondToRequests = new ManualResetEventSlim(true);
        private TimeSpan RouteDelay;
        private List<ReceivedRequest> ReceivedRequests = new List<ReceivedRequest>();
        public Dictionary<string, string> QueryParameters = new Dictionary<string, string>();
        private DefinedResponse CurrentResponse = new DefinedResponse();
        private int NextResponseIndex = 0;
        private List<DefinedResponse> Responses;
        private ISerializer Serializer;

        public FluentConfigurator(string path, HttpVerb get, ISerializer serializer)
        {
            Serializer = serializer;
            HttpVerb = get;
            Path = path;
            Responses = new List<DefinedResponse>
            {
                CurrentResponse
            };
        }

        public HttpMethod Method { get; set; }
        public bool IsRegex { get; set; }

        public void AddReceivedRequest(ReceivedRequest request)
        {
            ReceivedRequests.Add(request);
        }

        public RouteConfigurer IsHandledBy(Func<ReceivedRequest, string> generateOutput)
        {
            CurrentResponse.AddDescriptionPart("Handled by function");
            CurrentResponse.HandlerFunction = generateOutput;
            return this;
        }


        public RouteConfigurer Responds<T>(T output)
        {
            Util.CheckForSerializer(Serializer);
            CurrentResponse.Output = Serializer.Serialize(output);
            return this;
        }


        public RouteConfigurer Responds(byte[] output)
        {
            CurrentResponse.AddDescriptionPart("With binary output");
            CurrentResponse.BinaryOutput = output;
            return this;
        }

        public RouteConfigurer WithCode(int code)
        {
            CurrentResponse.AddDescriptionPart("With code " + code);
            CurrentResponse.ResponseModifiers.Add(ctx => ctx.Response.StatusCode = code);
            return this;
        }

        public RouteConfigurer WithHeader(string headerName, string headerValue)
        {
            CurrentResponse.AddDescriptionPart("With header " + headerName + " = " + headerValue);
            CurrentResponse.ResponseModifiers.Add(ctx => ctx.Response.AddHeader(headerName, headerValue));
            return this;
        }

        public RouteConfigurer WithParameter(string key, string value)
        {
            QueryParameters.Add(key, value);
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
            CurrentResponse.Output = output;
            return this;
        }

        public RouteConfigurer MatchingRegex()
        {
            IsRegex = true;
            return this;
        }

        internal bool DoesRouteMatch(HttpListenerRequest contextRequest)
        {
            if (!Path.EndsWith("/") && !IsRegex) Path += "/";
            var requestPath = contextRequest.Url.LocalPath;
            if (!requestPath.EndsWith("/")) requestPath += "/";
            
            var queryString = contextRequest.QueryString;
            if (queryString.Count != QueryParameters.Count)
                return false;
            foreach (string s in queryString)
            {
                var allKeysMatch = queryString[s].Equals(QueryParameters[s]);
                if (!allKeysMatch)
                    return false;
            }

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

        public RouteConfigurer Responds()
        {
            return this;
        }

        public RouteConfigurer WithCookie(Cookie cookie)
        {
            CurrentResponse.AddDescriptionPart("With cookie " + cookie.Name + " = " + cookie.Value);
            CurrentResponse.ResponseModifiers.Add(ctx => ctx.Response.SetCookie(cookie));
            return this;
        }

        public IRouteHistory History()
        {
            return new RouteHistory(ReceivedRequests.AsReadOnly());
        }

        public RouteConfigurer ImmediatelyAborts()
        {
            CurrentResponse.ShouldImmediatelyDisconnect = true;
            return this;
        }

        public void ResetCurrentResponseIndex()
        {
            NextResponseIndex = 0;
        }

        public RouteSequenceConfigurer ThenResponds(string bodyText)
        {
            CurrentResponse = new DefinedResponse
            {
                Output = bodyText
            };
            Responses.Add(CurrentResponse);
            return this;
        }


        public RouteSequenceConfigurer ThenResponds()
        {
            ThenResponds("");
            return this;
        }

        private class RouteHistory : IRouteHistory
        {
            public RouteHistory(IReadOnlyList<ReceivedRequest> requests)
            {
                ReceivedRequests = requests;
            }
            public IReadOnlyList<ReceivedRequest> ReceivedRequests { get; }
        }

        internal DefinedResponse GetNextDefinedResponse()
        {
            if(NextResponseIndex >= Responses.Count)
                return Responses[Responses.Count - 1];
            var resp = Responses[NextResponseIndex];
            NextResponseIndex++;
            return resp;
        }
    }
}