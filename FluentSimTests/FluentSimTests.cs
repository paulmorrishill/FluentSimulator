using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using FluentSim;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using RestSharp;
using Shouldly;

namespace FluentSimTests
{
  public class FluentSimTests
  {
    private FluentSimulator Sim;
    private const string BaseAddress = "http://localhost:8019/";

    private class JsonConvertSerializer : ISerializer
    {
      public string Serialize<T>(T obj) => JsonConvert.SerializeObject(obj);
      public T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json);
    }
    
    [SetUp]
    public void SetUp()
    {
      Sim = new FluentSimulator(BaseAddress, new JsonConvertSerializer());
      Sim.Start();
    }

    [TearDown]
    public void TearDown()
    {
      RemoveCurrentSim();
    }

    [Test]
    public void CanMakeGetRequest()
    {
      Sim.Get("/test")
        .Responds("TEST");

      var resp = MakeGetRequest("/test");
      resp.Content.ShouldBe("TEST");
      resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static IRestResponse MakeGetRequest(string path, int timeout=50000)
    {
      var request = new RestRequest(path, Method.GET);
      var client = new RestClient(BaseAddress)
      {
        Timeout = timeout
      };
      var resp = client.Execute(request);
      return resp;
    }

    private static IRestResponse MakePostRequest(string path, object body, Dictionary<string, string> headers = null)
    {
      var verb = Method.POST;
      return MakeRequest(path, verb, body, headers);
    }

    private static IRestResponse MakeRequest(string path, Method verb, object body = null, Dictionary<string, string> headers = null)
    {
      var resp = MakeRawRequest(path, verb, body, headers);
      return resp;
    }

    private static IRestResponse MakeRawRequest(string path, Method verb, object body = null, Dictionary<string, string> headers = null)
    {
      var request = new RestRequest(path, verb);
      if (body != null) request.AddParameter("text/json", body, ParameterType.RequestBody);
      var client = new RestClient(BaseAddress);
      client.Timeout = 5000;
      if (headers != null)
      {
        foreach (var header in headers)
        {
          request.AddHeader(header.Key, header.Value);
        }
      }
      var resp = client.Execute(request);
      return resp;
    }

    [Test]
    public void CanMakeGetRequestsOnDifferentUrls()
    {
      Sim.Get("/test1").Responds("output1");
      Sim.Get("/test2").Responds("output2");

      MakeGetRequest("/test1").Content.ShouldBe("output1");
      MakeGetRequest("/test2").Content.ShouldBe("output2");
    }

    [Test]
    public void RequestingAUrlThatDoesNotExistReturns501()
    {
      MakeGetRequest("/testUrl").StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Test]
    public void CanMakeRequestAfterRequestingAUrlThatIsNotConfigured()
    {
      Sim.Get("/test").Responds("out");
      MakeGetRequest("/nonexistent");
      MakeGetRequest("/test").Content.ShouldBe("out");
    }

    [Test]
    public void WhenARouteEndsInASlashTheUrlIsRecognised()
    {
      Sim.Get("/test").Responds("out");
      MakeGetRequest("/test/").Content.ShouldBe("out");
    }

    [Test]
    public void WhenTheTargetEndsInSometingDifferentItDoesNotMatchTheConfiguredRoute()
    {
      Sim.Get("/test").Responds("out");
      MakeGetRequest("/test/somethingelse").StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Test]
    public void WhenTargetStartsWithSometingDifferentItDoesNotMatch()
    {
      Sim.Get("/test").Responds("out");
      MakeGetRequest("something/test").StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Test]
    public void WhenTheRouteIsADifferentVerbItIsNotMatched()
    {
      Sim.Get("/test").Responds("outhere");
      MakePostRequest("/test", "").StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Test]
    public void CanMakePostRequest()
    {
      Sim.Post("/test").Responds("outhere");
      MakePostRequest("/test", "").Content.ShouldBe("outhere");
    }

    [Test]
    public void ThePauseAndResumeWorks()
    {
      var route = Sim.Get("/path").Responds("SOMEOUTPUT");
      route.Pause();
      var timer = new Stopwatch();
      timer.Start();
      ResumeTheRouteInHalfASecond(route);
      MakeGetRequest("/path").Content.ShouldBe("SOMEOUTPUT");
      timer.Stop();
      timer.ElapsedMilliseconds.ShouldBeGreaterThan(499);
    }

    [Test]
    public void TheTimeDelayWorks()
    {
      Sim.Get("/path").Delay(TimeSpan.FromMilliseconds(500))
        .Responds("delayed");
      var stopwatch = new Stopwatch();
      stopwatch.Start();
      MakeGetRequest("/path").Content.ShouldBe("delayed");
      stopwatch.Stop();
      stopwatch.ElapsedMilliseconds.ShouldBeGreaterThan(500);
    }

    private static void ResumeTheRouteInHalfASecond(RouteConfigurer route)
    {
      new Thread(() =>
      {
        Thread.Sleep(500);
        route.Resume();
      }).Start();
    }

    [Test]
    public void CanRespondWithCodes()
    {
      Sim.Post("/test").Responds().WithCode(300);
      MakePostRequest("/test", "").StatusCode.ShouldBe(HttpStatusCode.Ambiguous);
    }

    [Test]
    public void CanCreateASequenceOfResponses()
    {
      Sim.Post("/test")
        .Responds().WithCode(400)
        .ThenResponds()
        .WithCode(500)
        .ThenResponds()
        .WithCode(200);
      
      MakePostRequest("/test", "").StatusCode.ShouldBe(HttpStatusCode.BadRequest);
      MakePostRequest("/test", "").StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
      MakePostRequest("/test", "").StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public void GivenASequenceOfEventsItReusesTheLastResponse()
    {
      Sim.Post("/test")
        .Responds().WithCode(400)
        .ThenResponds()
        .WithCode(500);
      
      MakePostRequest("/test", "").StatusCode.ShouldBe(HttpStatusCode.BadRequest);
      MakePostRequest("/test", "").StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
      MakePostRequest("/test", "").StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
      MakePostRequest("/test", "").StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Test]
    public void CanOutputASequenceOfDifferentBodies()
    {
      Sim.Post("/test")
        .Responds("first")
        .ThenResponds("second");
      
      MakePostRequest("/test", "").Content.ShouldBe("first");
      MakePostRequest("/test", "").Content.ShouldBe("second");
    }
    
    [Test]
    public void CanResetRouteSequenceCount()
    {
      var route = Sim.Post("/test")
        .Responds("first")
        .ThenResponds("second");
      
      MakePostRequest("/test", "").Content.ShouldBe("first");
      route.ResetCurrentResponseIndex();
      MakePostRequest("/test", "").Content.ShouldBe("first");
    }
    
    [Test]
    public void CanRespondWithHeaders()
    {
      Sim.Post("/test").Responds().WithHeader("ThisHeader", "ThisValue");
      var resp = MakePostRequest("/test", "");

      // the response will contain a few default headers,
      // make sure it contains the custom header from above, too
      var customHeader = resp.Headers.Single(header => header.Name.Equals("ThisHeader"));
      Assert.NotNull(customHeader);
      Assert.AreEqual("ThisValue", customHeader.Value);
    }

    [Test]
    public void CanRespondWithCookies()
    {
      Sim.Get("/test").Responds().WithCookie(new Cookie("name", "VALTEST"));
      var resp = MakeGetRequest("/test");
      var cookie = resp.Cookies[0];
      cookie.Name.ShouldBe("name");
      cookie.Value.ShouldBe("VALTEST");
    }

    [Test]
    public void CanImmediatelyAbortConnection()
    {
      Sim.Get("/test").ImmediatelyAborts();
      var resp = MakeGetRequest("/test", 100);
      resp.StatusCode.ShouldBe((HttpStatusCode)0);
    }

    [Test]
    public void CanReturnSerialisedObjects()
    {
      Sim.Post("/test")
        .Responds(new TestObject());

      MakePostRequest("/test", "").Content.ShouldBe(@"{""TestField"":""ThisValue""}");
    }

    [Test]
    public void CanGetBinaryDataObject()
    {
      Sim.Post("/test")
        .Responds(new byte[] {1, 2, 3, 4});

      MakeRawRequest("/test", Method.POST).RawBytes.ShouldBe(new byte[] {1, 2, 3, 4});
    }

    [Test]
    public void CanUseCustomSerializer()
    {
      RemoveCurrentSim();
      var serializer = Substitute.For<ISerializer>();
      serializer.Serialize(Arg.Any<TestSerializeClass>())
        .Returns(callInfo => callInfo.Arg<TestSerializeClass>().ToString());
      Sim = new FluentSimulator(BaseAddress, serializer);
      Sim.Start();

      Sim.Post("/test")
        .Responds(new TestSerializeClass());

      MakePostRequest("/test", "").Content.ShouldBe("ToStringCalledCorrectly");
    }

    [Test]
    public void CanMatchRequestCaseInsensitively()
    {
      Sim.Get("/test").Responds("out");
      MakeGetRequest("/TEST").Content.ShouldBe("out");
    }

    [Test]
    public void CanMakeOtherVerbRequests()
    {
      MakeVerbRequest(Sim.Post("/test"), Method.POST);
      MakeVerbRequest(Sim.Get("/test"), Method.GET);
      MakeVerbRequest(Sim.Delete("/test"), Method.DELETE);
      MakeVerbRequest(Sim.Head("/test"), Method.HEAD);
      MakeVerbRequest(Sim.Merge("/test"), Method.MERGE);
      MakeVerbRequest(Sim.Options("/test"), Method.OPTIONS);
      MakeVerbRequest(Sim.Patch("/test"), Method.PATCH);
      MakeVerbRequest(Sim.Put("/test"), Method.PUT);
    }

    [Test]
    public void CanMakeRegexRequest()
    {
      Sim.Get("/test[0-9]").MatchingRegex().Responds("output1");
      MakeGetRequest("/test1").Content.ShouldBe("output1");
    }

    [Test]
    public void CanMakeComplexRegexRequest()
    {
      Sim.Get(@"api\/.*\/read\/Business\/Business.*").MatchingRegex().Responds("Output");
      MakeGetRequest("/api/02b9863e-cb07-c62d-50a9-8ecb40aa85b6/read/Business/Business/").Content.ShouldBe("Output");
    }

    [Test]
    public void CanMatchRegexNotEndingInSlash()
    {
      Sim.Get("/test[0-9]").MatchingRegex().Responds("output1");
      MakeGetRequest("/test1sdfsdfds").Content.ShouldBe("output1");
    }

    private void MakeVerbRequest(RouteConfigurer configurer, Method verb)
    {
      if (verb == Method.HEAD)
      {
        configurer.Responds("");
        MakeRequest("/test", verb).StatusCode.ShouldBe(HttpStatusCode.OK);
        return;
      }

      configurer.Responds(verb + "output");
      MakeRequest("/test", verb).Content.ShouldBe(verb + "output");
    }

    [Test]
    public void CanGetPreviousRequests()
    {
      Sim.Post("/post").Responds("OK");
      MakePostRequest("/post", "BODY", new Dictionary<string, string>
      {
        {"Header1", "value1"}
      });
      
      MakePostRequest("/post", "BODY", new Dictionary<string, string>
      {
        {"Header1", "value2"}
      });

      var requests = Sim.ReceivedRequests;
      requests.Count.ShouldBe(2);
      var firstRequest = requests[0];
      var secondRequest = requests[1];
      firstRequest.Headers["Header1"].ShouldBe("value1");
      secondRequest.Headers["Header1"].ShouldBe("value2");
    }

    [Test]
    public void CanGetMultipleRequestsWithHeaders()
    {
      Sim.Post("/post").Responds("OK");
      MakePostRequest("/post", "BODY");

      var requests = Sim.ReceivedRequests;
      requests.Count.ShouldBe(1);
      var firstRequest = requests[0];
      firstRequest.AcceptTypes.Length.ShouldBe(6);
      firstRequest.Url.AbsoluteUri.ShouldBe("http://localhost:8019/post");
      firstRequest.UserAgent.ShouldStartWith("RestSharp");
    }

    [Test]
    public void CanGetTimeOfPreviousRequest()
    {
      Sim.Post("/post").Responds("OK");
      MakePostRequest("/post", "BODY");
      var timeOfRequest = DateTime.Now;

      Thread.Sleep(300);

      var requests = Sim.ReceivedRequests;
      var firstRequest = requests[0];
      var diff = (firstRequest.TimeOfRequest - timeOfRequest);
      Math.Abs(diff.TotalMilliseconds).ShouldBeLessThan(100);
    }

    [Test]
    public void CanGetPreviousRequestBodyAsString()
    {
      Sim.Post("/post").Responds("OK");
      MakePostRequest("/post", "BODY");

      var requests = Sim.ReceivedRequests;
      var firstRequest = requests[0];
      firstRequest.RequestBody.ShouldBe("BODY");
    }

    [Test]
    public void CanGetPreviousRequestBodyAsObject()
    {
      Sim.Post("/post").Responds("OK");
      MakePostRequest("/post", @"{""TestField"":""TESTHERE""}");

      var requests = Sim.ReceivedRequests;
      var firstRequest = requests[0];
      firstRequest.BodyAs<TestObject>().TestField.ShouldBe("TESTHERE");
    }

    [Test]
    public void CanGetPreviousBodyWithCustomSerializer()
    {
      RemoveCurrentSim();
      var serializer = Substitute.For<ISerializer>();
      serializer.Deserialize<TestObject>(Arg.Any<string>())
        .Returns(_ => new TestObject {TestField = "REPLACEMENT"});
      Sim = new FluentSimulator(BaseAddress, serializer);
      Sim.Start();

      Sim.Post("/test");

      MakePostRequest("/test", @"{""TestField"":""original""}");

      Sim.ReceivedRequests[0].BodyAs<TestObject>().TestField.ShouldBe("REPLACEMENT");
    }

    private void RemoveCurrentSim()
    {
      Sim?.Stop();
      Sim?.Dispose();
    }

    [Test]
    public void CanAddCorsHeaders()
    {
      Sim.EnableCors();
      Sim.Post("/test");
      var resp = MakePostRequest("/test", @"{""TestField"":""original""}");
      AssertResponseContainsCorsHeaders(resp);
    }

    private static void AssertResponseContainsCorsHeaders(IRestResponse resp)
    {
      resp.Headers.First(n => n.Name == "Access-Control-Allow-Origin").Value.ShouldBe("*");
      resp.Headers.First(n => n.Name == "Access-Control-Allow-Headers").Value
        .ShouldBe("Authorization, Content-Type");
    }

    [Test]
    public void CanRespondToPreflightWithCorsHeaders()
    {
      Sim.EnableCors();
      Sim.Post("/test").Responds("TEST OUTPUT");
      var resp = MakeRequest("/test", Method.OPTIONS);
      AssertResponseContainsCorsHeaders(resp);

      MakePostRequest("/test", new object()).Content.ShouldBe("TEST OUTPUT");
    }

    [Test]
    public void CanGetTheRequestsToAMatchingRoute()
    {
      var post = Sim.Post("/test").Responds("POST").History();
      var get = Sim.Get("/test").Responds("GET").History();
      MakePostRequest("/test", @"{""TestField"":""original""}");
      MakeRequest("/test", Method.GET);
      MakeRequest("/test", Method.GET);
      get.ReceivedRequests.Count.ShouldBe(2);
      post.ReceivedRequests.Count.ShouldBe(1);
      post.ReceivedRequests[0].BodyAs<TestObject>().TestField.ShouldBe("original");
    }

    [Test]
    public void SequentialSetupsOverwriteThePreviousSetups()
    {
      Sim.Post("/post").Responds("OK");
      Sim.Post("/post").Responds("OK2");
      MakePostRequest("/post", "").Content.ShouldBe("OK2");
    }

    [Test]
    public void CanMakeGetRequestWithQueryString()
    {
      var queryString = "/test?key=value";
      Sim.Get("/test").WithParameter("key", "value").Responds("OK");
      var result = MakeGetRequest(queryString);
      result.Content.ShouldBe("OK");
    }

    [Test]
    public void CanMakeGetRequestWithMultipleQueryStringsOutOfOrder()
    {
      var queryString = "/test?key=value&key1=value1&key2=value2";
      Sim.Get("/test")
        .WithParameter("key", "value")
        .WithParameter("key2", "value2")
        .WithParameter("key1", "value1")
        .Responds("OK");
      var result = MakeGetRequest(queryString);
      result.Content.ShouldBe("OK");
    }

    [Test]
    public void GivenTheQueryStringIsLongerThanWhatWasExpected_Fails()
    {
      var queryString = "/test?key=value&key1=value1&key2=value2";
      Sim.Get("/test")
        .WithParameter("key1", "value1")
        .WithParameter("key", "value")
        .Responds("OK");
      var result = MakeGetRequest(queryString);
      result.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Test]
    public void GivenTheExpectedParametersAreLongerThanTheQueryString_Fails()
    {
      var queryString = "/test?key=value";
      Sim.Get("/test")
        .WithParameter("key", "value")
        .WithParameter("key2", "value2")
        .WithParameter("key1", "value1")
        .Responds("OK");
      var result = MakeGetRequest(queryString);
      result.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Test]
    public void GivenTheQueryParameterCountIsSameButParametersDifferentItDoesNotMatch()
    {
      var queryString = "/test?unexpected=value";
      Sim.Get("/test")
        .WithParameter("key", "value")
        .Responds("OK");
      var result = MakeGetRequest(queryString);
      result.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [TestCase("value%20here", "value here")]
    [TestCase("value+here", "value here")]
    [TestCase("Some%25%5E%26*(value", "Some%^&*(value")]
    public void GivenTheQueryParamsHaveUrlEncodedCharactersItComparesOnUrlDecodedValues(string encoded, string decoded)
    {
      var queryString = "/test?key=" + encoded;
      Sim.Get("/test")
        .WithParameter("key", decoded)
        .Responds("OK");
      var result = MakeGetRequest(queryString);
      result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
    
    [Test]
    public void CanUseLambdaFunctionToRespondAtRuntime()
    {
      var counter = 0;
      Sim.Post("/post").IsHandledBy(r => $"Counter: {counter++}");
      
      var resp1 = MakePostRequest("/post", "BODY");
      var resp2 = MakePostRequest("/post", "BODY");
      resp1.Content.ShouldBe("Counter: 0");
      resp2.Content.ShouldBe("Counter: 1");
    }
    
    [Test]
    public void LambdaHandlerReceivesCorrectRequestInfo()
    {
      Sim.Post("/post").IsHandledBy(r => $"Req: {r.RequestBody}");
      
      var resp1 = MakePostRequest("/post", "BODY");
      resp1.Content.ShouldBe("Req: BODY");
    }

    [Test]
    public void ThrowsCorrectErrorMessageIfNoSerializerProvided()
    {
      using var simulator = new FluentSimulator(BaseAddress);
      var ex = Assert.Throws<SimulatorException>(() =>
      {
        simulator.Post("/test")
          .Responds(new TestSerializeClass());
      });
      ex.Message.ShouldBe("No serializer has been provided, before using the serialization methods make sure to provide a serializer in the constructor of the simulator");  
    }

    [Test]
    public void ThrowsCorrectErrorMessageIfNoSerializerProvidedWhenDeserializing()
    {
      RemoveCurrentSim();
      Sim = new FluentSimulator(BaseAddress);
      Sim.Start();
      var ex = Assert.Throws<SimulatorException>(() =>
      {
        Sim.Post("/post").Responds("OK");
        MakePostRequest("/post", @"{""TestField"":""TESTHERE""}");
        var requests = Sim.ReceivedRequests;
        var firstRequest = requests[0];
        firstRequest.BodyAs<TestObject>().TestField.ShouldBe("TESTHERE");
      });
      ex.Message.ShouldBe("No serializer has been provided, before using the serialization methods make sure to provide a serializer in the constructor of the simulator");  
    }

    [Test]
    public void ThrowsListenerExceptionsAfterStop()
    {
      Sim.Post("/post")
        .IsHandledBy(r => throw new Exception("Inner exception"));
      MakePostRequest("/post", "{}");
      MakePostRequest("/post", "{}");

      var ex = Assert.Throws<AggregateException>(() =>
      {
        Sim.Stop();
      });
      ex.InnerExceptions.Count.ShouldBe(2);
      ex.InnerExceptions[0].Message.ShouldBe("Inner exception");
      ex.InnerExceptions[1].Message.ShouldBe("Inner exception");
      
      Sim.Dispose();
      Sim = null;
    }

    [Test]
    public void DoesNotBlowUpWhenConcurrentlyReadingReceivedRequestsWhileMakingThem()
    {
      RemoveCurrentSim();
      Sim = new FluentSimulator(BaseAddress, concurrentListeners:50);
      Sim.Start();
      Sim.Post("/post").Responds("OK");

      var threadCount = 50;
      var requestsPerThread = 100;
      var countDown = new CountdownEvent(threadCount);
      var threads = new List<Thread>();
      for (var i = 0; i < threadCount; i++)
      {
        var requestThread = new Thread(() =>
        {
          for (var r = 0; r < requestsPerThread; r++)
          {
            var resp = MakePostRequest("/post", "{}");
            resp.StatusCode.ShouldBe(HttpStatusCode.OK);
          }

          countDown.Signal();
        });
        threads.Add(requestThread);
        requestThread.Start();
      }

      var readThread = new Thread(() =>
      {
        while (!countDown.IsSet)
        {
          var receivedRequests = Sim.ReceivedRequests;
          var s = receivedRequests.Select(r => r.RequestBody);
        }
      });

      readThread.Start();
      readThread.Join();
      countDown.Wait();
      threads.ForEach(t => t.Join());
      Sim.ReceivedRequests.Count.ShouldBe(threadCount * requestsPerThread);
    }

    [Test]
    public void Returns500OnListenerException()
    {
      Sim.Post("/post")
        .IsHandledBy(r => throw new Exception("Inner exception"));

      var resp = MakePostRequest("/post", "{}");
      resp.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
      resp.Content.ShouldBe("Inner exception");

      try
      {
        // A listener exception will be thrown here
        Sim.Stop();
      }
      catch (Exception)
      {
        Sim.Dispose();
        Sim = null;
      }
    }

    private class AllFieldsReplacementConverter : JsonConverter
    {
      public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
      {
      }

      public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
        JsonSerializer serializer)
      {
        return "REPLACEMENT";
      }

      public override bool CanConvert(Type objectType)
      {
        return objectType == typeof(string);
      }
    }

    public class TestObject
    {
      public string TestField = "ThisValue";
    }

    public class TestSerializeClass
    {
      public override string ToString()
      {
        return "ToStringCalledCorrectly";
      }
    }
  }
}