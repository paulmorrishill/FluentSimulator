using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
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
            var request = context.Request;

            var matchingRoute = ConfiguredRoutes.FirstOrDefault(route => route.DoesRouteMatch(context.Request));
            var body = new StreamReader(request.InputStream).ReadToEnd();

            IncomingRequests.Add(new ReceivedRequest(JsonSerializer)
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
                RequestBody = body
            });

            if (matchingRoute == null)
            {
                response.StatusCode = 501;
                response.Close();
                BeginGetContext();
                return;
            }

            matchingRoute.WaitUntilReadyToRespond();
            matchingRoute.RunContextModifiers(context);

            byte[] buffer = Encoding.UTF8.GetBytes(matchingRoute.GetBody());

            response.ContentLength64 = buffer.Length;
            var output = response.OutputStream;
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

    public class ReceivedRequest
    {
        private JsonSerializerSettings JsonSerializerSettings;

        public ReceivedRequest(JsonSerializerSettings jsonSerializer)
        {
            JsonSerializerSettings = jsonSerializer;
        }

        public Uri Url { get; set; }
        public string HttpMethod { get; set; }
        public Encoding ContentEncoding { get; set; }
        public string[] AcceptTypes { get; set; }
        public string ContentType { get; set; }
        public NameValueCollection Headers { get; set; }
        public CookieCollection Cookies { get; set; }
        public NameValueCollection QueryString { get; set; }
        public string RawUrl { get; set; }
        public string UserAgent { get; set; }
        public string[] UserLanguage { get; set; }
        public string RequestBody { get; set; }

        public T GetBodyAs<T>()
        {
            return JsonConvert.DeserializeObject<T>(RequestBody, JsonSerializerSettings);
        }
    }
}