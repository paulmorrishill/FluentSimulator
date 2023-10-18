using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;

namespace FluentSim
{
    [DebuggerDisplay("{Description}")]
    public class DefinedResponse
    {
        public string Output = "";
        private string Description { get; set; }
        public void AddDescriptionPart(string part) => Description += " " + part;
        public byte[] BinaryOutput = null;
        public bool ShouldImmediatelyDisconnect = false;
        public List<Action<HttpListenerContext>> ResponseModifiers = new List<Action<HttpListenerContext>>();
        
        internal string GetBody()
        {
            return Output;
        }
        
        internal void RunContextModifiers(HttpListenerContext context)
        {
            foreach (var responseModifier in ResponseModifiers)
                responseModifier(context);
        }
    }
    
    public class FluentConfigurator : RouteConfigurer, RouteSequenceConfigurer
    {
        private string Path;
        private HttpVerb HttpVerb;
        private ManualResetEventSlim RespondToRequests = new ManualResetEventSlim(true);
        private TimeSpan RouteDelay;
        private JsonSerializerSettings JsonSerializerSettings;
        private List<ReceivedRequest> ReceivedRequests = new List<ReceivedRequest>();
        public Dictionary<string, string> QueryParameters = new Dictionary<string, string>();
        private DefinedResponse CurrentResponse = new DefinedResponse();
        private int NextResponseIndex = 0;
        private List<DefinedResponse> Responses;
        private Func<ReceivedRequest, string> HandlerFunction { get; set; }

        public FluentConfigurator(string path, HttpVerb get, JsonSerializerSettings jsonConverter)
        {
            JsonSerializerSettings = jsonConverter;
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
            HandlerFunction = generateOutput;
            return this;
        }


        public RouteConfigurer Responds<T>(T output)
        {
            CurrentResponse.Output = JsonConvert.SerializeObject(output, JsonSerializerSettings);
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

        internal string GetBody(ReceivedRequest request)
        {
            if (HandlerFunction != null)
                return HandlerFunction(request);
            return Output;
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

        public DefinedResponse GetNextDefinedResponse()
        {
            if(NextResponseIndex >= Responses.Count)
                return Responses[Responses.Count - 1];
            var resp = Responses[NextResponseIndex];
            NextResponseIndex++;
            return resp;
        }
    }

}