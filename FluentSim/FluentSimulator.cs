using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace FluentSim
{
    public class FluentSimulator : IDisposable
    {
        private string Address;
        private List<FluentConfigurator> ConfiguredRoutes = new List<FluentConfigurator>();
        private HttpListener HttpListener;
        private readonly List<Exception> ListenerExceptions = new List<Exception>();
        public object ListenerExceptionsLock = new object();

        public List<ReceivedRequest> IncomingRequests = new List<ReceivedRequest>();
        private ISerializer Serializer;
        public IReadOnlyList<ReceivedRequest> ReceivedRequests => IncomingRequests.AsReadOnly();
        private bool CorsEnabled { get; set; }
        
        public FluentSimulator(string address)
        {
            Address = address;
        }

        public FluentSimulator(string address, ISerializer serializer)
        {
            Serializer = serializer;
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
                lock (ListenerExceptionsLock)
                    ListenerExceptions.Add(e);
            }
            finally
            {
                if(HttpListener.IsListening)
                    BeginGetContext();
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
                return;
            }

            var matchingRoute = ConfiguredRoutes.FirstOrDefault(route => route.DoesRouteMatch(context.Request));
            var receivedRequest = GetReceivedRequest(request);
            IncomingRequests.Add(receivedRequest);

            if (matchingRoute == null)
            {
                response.StatusCode = 501;
                response.Close();
                return;
            }
            var definedResponse = matchingRoute.GetNextDefinedResponse();

            if (definedResponse.ShouldImmediatelyDisconnect)
            {
                response.Abort();
                return;
            }

            matchingRoute.AddReceivedRequest(receivedRequest);
            matchingRoute.WaitUntilReadyToRespond();

            definedResponse.RunContextModifiers(context);

            byte[] buffer = definedResponse.BinaryOutput;

            if(buffer == null)
                buffer = Encoding.UTF8.GetBytes(definedResponse.GetBody(receivedRequest));

            response.ContentLength64 = buffer.Length;
            var output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
        }

        private ReceivedRequest GetReceivedRequest(HttpListenerRequest request)
        {
            var body = new StreamReader(request.InputStream).ReadToEnd();

            var receivedRequest = new ReceivedRequest(Serializer)
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
            var routeConfig = new FluentConfigurator(path, verb, Serializer);
            ConfiguredRoutes.Insert(0, routeConfig);
            return routeConfig;
        }

        public void Stop()
        {
            lock(ListenerExceptionsLock)
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

        public void Dispose()
        {
            ((IDisposable) HttpListener)?.Dispose();
        }
    }
}