using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace FluentSim
{
    public class FluentSimulator : IDisposable
    {
        private readonly string Address;
        private readonly object ConfiguredRoutesLock = new object();
        private readonly List<FluentConfigurator> ConfiguredRoutes = new List<FluentConfigurator>();
        private HttpListener HttpListener;
        private readonly object HttpListenerLock = new object();
        private readonly List<Exception> ListenerExceptions = new List<Exception>();
        private readonly object ListenerExceptionsLock = new object();
        private readonly object ReceivedRequestsLock = new object();
        private readonly List<ReceivedRequest> IncomingRequests = new List<ReceivedRequest>();
        private readonly ISerializer Serializer;
        private readonly SemaphoreSlim ConcurrentRequestCounter;
        private CancellationTokenSource ListeningCancellationTokenSource;

        public IReadOnlyList<ReceivedRequest> ReceivedRequests
        {
            get
            {
                lock(ReceivedRequestsLock)
                    return IncomingRequests.AsReadOnly();
            }
        }

        private bool CorsEnabled { get; set; }
        
        public FluentSimulator(string address, ISerializer serializer = null, int concurrentListeners = 10)
        {
            ConcurrentRequestCounter = new SemaphoreSlim(concurrentListeners);            
            Serializer = serializer;
            Address = address;
        }

        public void Start()
        {
            lock (HttpListenerLock)
            {
                HttpListener = new HttpListener();
                HttpListener.Prefixes.Add(Address);
                HttpListener.Start();
                ListeningCancellationTokenSource = new CancellationTokenSource();
                var queuingThread = new Thread(() =>
                {
                    while (ListeningCancellationTokenSource.IsCancellationRequested == false)
                    {
                        try
                        {
                            ConcurrentRequestCounter.Wait(ListeningCancellationTokenSource.Token);
                            BeginGetContext();
                        }
                        catch (OperationCanceledException e)
                        {
                            // This is expected when the listener is stopped
                        }
                    }
                });
                queuingThread.Start();
            }
        }

        private void ProcessRequest(IAsyncResult ar)
        {
            ConcurrentRequestCounter.Release();
            try
            {
                TryToProcessRequest(ar);
            }
            catch (Exception e)
            {
                lock (ListenerExceptionsLock)
                    ListenerExceptions.Add(e);
            }
        }

        private void TryToProcessRequest(IAsyncResult ar)
        {
            lock (HttpListenerLock)
            {
                if (!HttpListener.IsListening)
                    return;
            }
            var context = ((HttpListener) ar.AsyncState).EndGetContext(ar);
            var response = context.Response;
            var request = context.Request;

            try
            {
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

                FluentConfigurator matchingRoute;
                lock(ConfiguredRoutesLock)
                    matchingRoute = ConfiguredRoutes.FirstOrDefault(route => route.DoesRouteMatch(context.Request));
                var receivedRequest = GetReceivedRequest(request);
                lock(ReceivedRequestsLock)
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
            } catch (Exception e)
            {
                lock (ListenerExceptionsLock)
                    ListenerExceptions.Add(e);
                response.StatusCode = 500;
                response.OutputStream.Write(Encoding.UTF8.GetBytes(e.Message), 0, e.Message.Length);
                response.Close();
            }
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
            lock(HttpListener)
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
            lock (ConfiguredRoutesLock)
            {
                ConfiguredRoutes.Insert(0, routeConfig);
            }
            return routeConfig;
        }

        public void Stop()
        {
            ListeningCancellationTokenSource.Cancel();
            lock (HttpListenerLock)
            {
                HttpListener.Stop();
            }
            
            lock(ListenerExceptionsLock)
                if (ListenerExceptions.Any())
                    throw new AggregateException(ListenerExceptions);
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
            lock (HttpListenerLock)
            {
                ((IDisposable) HttpListener)?.Dispose();
            }
        }
    }
}