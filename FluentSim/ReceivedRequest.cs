using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace FluentSim
{
    public class ReceivedRequest
    {
        private JsonSerializerSettings JsonSerializerSettings;
        public DateTime TimeOfRequest;

        public ReceivedRequest(JsonSerializerSettings jsonSerializer)
        {
            JsonSerializerSettings = jsonSerializer;
        }

        public Uri Url { get; internal set; }
        public string HttpMethod { get; internal set; }
        public Encoding ContentEncoding { get; internal set; }
        public string[] AcceptTypes { get; internal set; }
        public string ContentType { get; internal set; }
        public NameValueCollection Headers { get; internal set; }
        public CookieCollection Cookies { get; internal set; }
        public NameValueCollection QueryString { get; internal set; }
        public string RawUrl { get; internal set; }
        public string UserAgent { get; internal set; }
        public string[] UserLanguage { get; internal set; }
        public string RequestBody { get; internal set; }

        public T BodyAs<T>()
        {
            return JsonConvert.DeserializeObject<T>(RequestBody, JsonSerializerSettings);
        }
    }
}