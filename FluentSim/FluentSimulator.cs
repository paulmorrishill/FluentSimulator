using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading;
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
        public List<ReceivedRequest> IncomingRequests = new List<ReceivedRequest>();
        public IReadOnlyList<ReceivedRequest> ReceivedRequests => IncomingRequests.AsReadOnly();
        private bool CorsEnabled { get; set; }

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
                ListenerExceptions.Add(e);
            }
        }

        private void TryToProcessRequest(IAsyncResult ar)
        {
            var context = ((HttpListener) ar.AsyncState).EndGetContext(ar);
            var response = context.Response;
            var request = context.Request;

            if (CorsEnabled)
            {
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Headers", "Authorization, Content-Type");
            }

            if (CorsEnabled && request.HttpMethod.ToUpperInvariant() == "OPTIONS")
            {
                response.Close();
                BeginGetContext();
                return;
            }

            var matchingRoute = ConfiguredRoutes.FirstOrDefault(route => route.DoesRouteMatch(context.Request));
            var receivedRequest = GetReceivedRequest(request);
            IncomingRequests.Add(receivedRequest);

            if (matchingRoute == null)
            {
                response.StatusCode = 501;
                response.Close();
                BeginGetContext();
                return;
            }

            matchingRoute.AddReceivedRequest(receivedRequest);
            matchingRoute.WaitUntilReadyToRespond();

            matchingRoute.RunContextModifiers(context);

            byte[] buffer = matchingRoute.BinaryOutput;

            if(buffer == null)
                buffer = Encoding.UTF8.GetBytes(matchingRoute.GetBody());

            response.ContentLength64 = buffer.Length;
            var output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
            BeginGetContext();
        }

        private ReceivedRequest GetReceivedRequest(HttpListenerRequest request)
        {
            var body = new StreamReader(request.InputStream).ReadToEnd();

            var receivedRequest = new ReceivedRequest(JsonSerializer)
            {
                Url = request.Url,
                HttpMethod = request.HttpMethod,
                AcceptTypes = request.AcceptTypes,
                ContentEncoding = request.ContentEncoding,
                ContentType = request.ContentType,
                Cookies = request.Cookies,
                Headers = request.Headers,
                QueryString = request.QueryString,
                RawUrl = request.RawUrl,
                UserAgent = request.UserAgent,
                UserLanguage = request.UserLanguages,
                RequestBody = body,
                TimeOfRequest = DateTime.Now
            };
            return receivedRequest;
        }

        private void BeginGetContext()
        {
            HttpListener.BeginGetContext(ProcessRequest, HttpListener);
        }

        public RouteConfigurer Post(string path)
        {
            return InitialiseRoute(path, HttpVerb.Post);
        }

        public RouteConfigurer Get(string path)
        {
            return InitialiseRoute(path, HttpVerb.Get);
        }

        private RouteConfigurer InitialiseRoute(string path, HttpVerb verb)
        {
            var routeConfig = new FluentConfigurator(path, verb, JsonSerializer);
            ConfiguredRoutes.Insert(0, routeConfig);
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


        public void EnableCors()
        {
            CorsEnabled = true;
        }

    }
}