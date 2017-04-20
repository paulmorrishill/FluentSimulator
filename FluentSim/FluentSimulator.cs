using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using Newtonsoft.Json;

namespace FluentSim
{
    public class FluentSimulator
    {
        private string Address;
        private List<FluentConfigurator> ConfiguredRoutes = new List<FluentConfigurator>();
        private HttpListener HttpListener;
        private List<Exception> ListenerExceptions = new List<Exception>();
        private JsonSerializerSettings JsonSerializer;

        public FluentSimulator(string address)
        {
            Address = address;
            JsonSerializer = new JsonSerializerSettings();
        }

        public FluentSimulator(string address, JsonSerializerSettings serializer)
        {
            Address = address;
            JsonSerializer = serializer;
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
            matchingRoute.RunContextModifiers(context);

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(matchingRoute.GetBody());

            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
            BeginGetContext();
        }

        private void BeginGetContext()
        {
            HttpListener.BeginGetContext(ProcessRequest, HttpListener);
        }

        public RouteConfigurer Post(string path)
        {
            var routeConfig = new FluentConfigurator(path, HttpVerb.Post, JsonSerializer);
            ConfiguredRoutes.Add(routeConfig);
            return routeConfig;
        }

        public RouteConfigurer Get(string path)
        {
            return InitialiseRoute(path, HttpVerb.Get);
        }

        private RouteConfigurer InitialiseRoute(string path, HttpVerb verb)
        {
            var routeConfig = new FluentConfigurator(path, verb, JsonSerializer);
            ConfiguredRoutes.Add(routeConfig);
            return routeConfig;
        }

        public void Stop()
        {
            if (ListenerExceptions.Any())
                throw new SimulatorException(ListenerExceptions);
            HttpListener.Stop();
        }

        public RouteConfigurer Delete(string routePath)
        {
            return InitialiseRoute(routePath, HttpVerb.Delete);
        }

        public RouteConfigurer Head(string routePath)
        {
            return InitialiseRoute(routePath, HttpVerb.Head);
        }

        public RouteConfigurer Merge(string routePath)
        {
            return InitialiseRoute(routePath, HttpVerb.Merge);
        }

        public RouteConfigurer Options(string routePath)
        {
            return InitialiseRoute(routePath, HttpVerb.Options);
        }

        public RouteConfigurer Patch(string routePath)
        {
            return InitialiseRoute(routePath, HttpVerb.Patch);
        }

        public RouteConfigurer Put(string routePath)
        {
            return InitialiseRoute(routePath, HttpVerb.Put);
        }
    }
}