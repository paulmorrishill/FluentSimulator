using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace FluentSim
{
    public class FluentSimulator
    {
        private string Address;
        private List<FluentConfigurator> ConfiguredRoutes = new List<FluentConfigurator>();
        private HttpListener HttpListener;
        private List<Exception> ListenerExceptions = new List<Exception>();

        public FluentSimulator(string address)
        {
            Address = address;
        }

        public void Start()
        {
            HttpListener = new HttpListener();
            HttpListener.Prefixes.Add(Address);
            HttpListener.Start();
            HttpListener.BeginGetContext(ProcessRequest, HttpListener);
        }

        private void ProcessRequest(IAsyncResult ar)
        {
            try
            {
                TryToProcessRequest(ar);
            }
            catch (Exception e)
            {
                Stop();
                ListenerExceptions.Add(e);
            }
        }

        private void TryToProcessRequest(IAsyncResult ar)
        {
            var context = ((HttpListener) ar.AsyncState).EndGetContext(ar);
            var response = context.Response;

            var matchingRoute = ConfiguredRoutes.FirstOrDefault(route => route.DoesRouteMatch(context.Request));
            if (matchingRoute == null)
            {
                response.StatusCode = 501;
                response.Close();
                BeginGetContext();
                return;
            }

            matchingRoute.WaitUntilReadyToRespond();
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(matchingRoute.GetBody());

            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            // You must close the output stream.
            output.Close();
            BeginGetContext();
        }

        private void BeginGetContext()
        {
            HttpListener.BeginGetContext(ProcessRequest, HttpListener);
        }

        public RouteConfigurer Post(string path)
        {
            var routeConfig = new FluentConfigurator(path, HttpVerb.Post);
            ConfiguredRoutes.Add(routeConfig);
            return routeConfig;
        }

        public RouteConfigurer Get(string path)
        {
            var routeConfig = new FluentConfigurator(path, HttpVerb.Get);
            ConfiguredRoutes.Add(routeConfig);
            return routeConfig;
        }

        public void Stop()
        {
            if (ListenerExceptions.Any())
                throw new SimulatorException(ListenerExceptions);
            HttpListener.Stop();
        }
    }

    public enum HttpVerb
    {
        Post,
        Get,
        Put,
        Patch,
        Head,
        Delete,
        Connect,
        Trace
    }

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

    public interface RouteConfigurer
    {
        RouteConfigurer Responds<T>(T output);
        RouteConfigurer Responds(string output);
        RouteConfigurer WithCode(int code);
        RouteConfigurer WithHeader(string headerName, string headerValue);
        RouteConfigurer Delay(TimeSpan routeDelay);
        RouteConfigurer Pause();
        RouteConfigurer Resume();
        RouteConfigurer WithBody<T>();
        RouteConfigurer WithBody(string body);
    }

    public class FluentConfigurator : RouteConfigurer
    {
        private object Output = "";
        private string Path;
        private HttpVerb HttpVerb;
        private ManualResetEventSlim RespondToRequests = new ManualResetEventSlim(true);
        private TimeSpan RouteDelay;

        public FluentConfigurator(string path, HttpVerb get)
        {
            HttpVerb = get;
            Path = path;
        }

        public HttpMethod Method { get; set; }

        public RouteConfigurer Responds<T>(T output)
        {
            Output = output;
            return this;
        }

        public RouteConfigurer WithCode(int code)
        {
            throw new System.NotImplementedException();
        }

        public RouteConfigurer WithHeader(string headerName, string headerValue)
        {
            throw new NotImplementedException();
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

        public RouteConfigurer WithBody<T>()
        {
            throw new NotImplementedException();
        }

        public RouteConfigurer WithBody(string body)
        {
            throw new NotImplementedException();
        }

        internal string GetBody()
        {
            return Output.ToString();
        }

        internal bool DoesRouteMatch(HttpListenerRequest contextRequest)
        {
            if (!Path.EndsWith("/")) Path += "/";
            var requestPath = contextRequest.Url.LocalPath;
            if (!requestPath.EndsWith("/")) requestPath += "/";

            var pathMatches = requestPath == Path;
            var verbMatches = HttpVerb.ToString().ToUpper() == contextRequest.HttpMethod;
            return pathMatches && verbMatches;
        }

        internal void WaitUntilReadyToRespond()
        {
            Thread.Sleep(RouteDelay);
            RespondToRequests.Wait();
        }
    }
}