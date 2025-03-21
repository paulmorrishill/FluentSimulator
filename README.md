# FluentSimulator
FluentSimulator is a .Net HTTP API simulator library that is extremely versitile and unassuming. 

There are 2 main use cases for FluentSimulator.

- Simulating a 3rd party API to unit test your code that interacts with that API
- Mocking out REST or other APIs during automated browser based testing using something like selenium - useful for light weight single page application testing.

[![Build status](https://ci.appveyor.com/api/projects/status/h1v3d192xhesks29?svg=true)](https://ci.appveyor.com/project/paulmorrishill/fluentsimulator)
[![AppVeyor tests](https://img.shields.io/appveyor/tests/paulmorrishill/fluentsimulator)](https://ci.appveyor.com/project/paulmorrishill/fluentsimulator/build/tests)
[![NuGet](https://img.shields.io/nuget/v/FluentSimulator.svg)](https://www.nuget.org/packages/FluentSimulator/)

# Usage
Using the simulator is designed to be extremely easy.

```c#
//Initialise the simulator
var simulator = new FluentSimulator("http://localhost:8080/");
simulator.Start();

//Setup your simulated routes and responses
simulator.Get("/my/route").Responds("Some example content here");

//Now make an HTTP request using whatever means
//GET http://localhost:8080/my/route
//200 Some example content here
```

Make sure to shutdown the simulator at the end of your unit test (usually in a teardown method) - 
this will prevent subsequent tests failing on the port being in use.


```c#
[TearDown]
public void TearDown()
{
   //Stop the simulator
   simulator.Stop();  
}
```

### Breaking change in V3
[Json.Net](https://nuget.org/packages/Newtonsoft.Json) is no longer a dependency in the package.

You will need to include it in your project if you are using the `Responds(object)` method. Or if you intend on using the `BodyAs<T>` method on the `ReceivedRequest` object.

This was done to reduce dependency version conflicts, and also for those who do not intend to use `JSON` e.g. for `XML` endpoint mocking.

To have it work exactly as in V2, simply provide an implementation of `ISerializer` that uses `JsonConvert` in the constructor of `FluentSimulator`.

```c#
private class JsonConvertSerializer : ISerializer
{
  public string Serialize<T>(T obj) => JsonConvert.SerializeObject(obj);
  public T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json);
}

Sim = new FluentSimulator(BaseAddress, new JsonConvertSerializer());
Sim.Start();
```

# Request matching
The simulator can be configured in a few different ways to choose the appropriate response to requests.

## Simple requests
Simple plain text path matching can be done with the specific verb methods. If the status code is not specified the simulator returns 200.

```c#
// Match http://localhost:8080/test
simulator.Post("/test").Responds("Hello World!");
simulator.Get("/test").Responds("Hello World!");
simulator.Delete("/test").Responds("Hello World!");
simulator.Head("/test").Responds("Hello World!");
simulator.Merge("/test").Responds("Hello World!");
simulator.Options("/test").Responds("Hello World!");
simulator.Patch("/test").Responds("Hello World!");
simulator.Put("/test").Responds("Hello World!");
simulator.Post("/").Responds("Hello World!");
    
// Match GET http://localhost:8080/some/extended/path/here.html
simulator.Get("/some/extended/path/here.html").Responds("Hello World!");
```

## Regex matching
You can match additionally by regex on any of the verb methods by appending the ```.MatchingRegex()``` call.

```c#
// Match on Regex
// e.g. GET http://localhost:8080/user/42/profile
simulator.Get("/user/[0-9]+/profile")
         .MatchingRegex()   
         .Responds("Super cool profile data")
```

## Matching on query parameters
You can also match on query parameters using the ```WithParameter``` method. 

- Parameter order does not matter
- Parameters are case sensitive for both key and value
- Query parameters should be passed in raw - the simulator deals with URL decoding

```c#
// Match on query parameters
// e.g. GET http://localhost:8080/viewprofile.php?id=123
simulator.Get("/viewprofile.php")
         .WithParameter("id", "123")
         .Responds("Some profile stuff");

 // Query parameters with special characters
 // e.g. GET http://localhost:8080/viewprofile.php?id=SOME-%C2%A3%24%25%5E%26-ID
 simulator.Get("/viewprofile.php")
          .WithParameter("id", "SOME-£$%^&-ID")
          .Responds("OK");
```

## Non-matching requests
Any request that does not match any configured responses will return a *501 Not Implemented* status code and an empty response body.

If you want to capture unexpected requests you can set the flag `ThrowOnUnexpectedRequest` to `true` on the simulator instance.

```c#
simulator.ThrowOnUnexpectedRequest = true;
```

Because all request handling is done asynchronously, the exceptions will be thrown on the `simulator.Stop()` method (the simulator will still stop as expected). An exception of type `UnexpectedRequestException` will be thrown with a comprehensive message of the unexpected requests that were made.

```text
One or more unexpected requests were received:
--------------------------------------------------
Time of Request: 18/03/2025 23:25:33
URL: http://localhost:8019/unexpected?q=test
HTTP Method: GET
Content Encoding: Unicode (UTF-8)
Content Type: 
Accept Types: application/json, text/json, text/x-json, text/javascript, application/xml, text/xml
User Agent: RestSharp/106.6.10.0
User Languages: 
Raw URL: /unexpected?q=test
Headers:
  Connection: Keep-Alive
  Accept: application/json, text/json, text/x-json, text/javascript, application/xml, text/xml
  Accept-Encoding: gzip, deflate
  Host: localhost:8019
  User-Agent: RestSharp/106.6.10.0
Query String Parameters:
  q: test
Cookies:
Request Body:
```

You can also access the unexpected requests via the `UnexpectedRequests` property on the `UnexpectedRequestException` instance.

```c#
try
{
    simulator.Stop();
}
catch (UnexpectedRequestException ex)
{
    // Do something with the unexpected requests
    var unexpectedRequests = ex.UnexpectedRequests;
}
```

# Responding to requests
The simulator can be very useful in testing outputs that aren't easy to create reliably on demand using real endpoints.

## Status codes
Want to see how your code handles 500 server errors, or 404s?

```c#
simulator.Get("/my/route").Responds().WithCode(500);
simulator.Get("/employee/44").Responds().WithCode(404);
```

## Headers
Arbitrary headers can be appended to the response.

```c#
simulator.Get("/employee/123")
         .Responds("{}")
         .WithHeader("Content-Type", "application/json")
         .WithHeader("X-Powered-By", "Rainbows and sunshine");
```

## Cookies
You can send cookies.

```c#
simulator.Post("/authenticate")
         .Responds()
         .WithCookie(new Cookie("Token", "ABCDEF"));
```

## Dynamic handlers
You can also use a dynamic handler to generate the response. This is useful for calculating responses based on the request data.

```c#
var counter = 0;
// Request is of type ReceivedRequest see below for more info
Sim.Post("/post").IsHandledBy(request => $"Counter: {counter++}");

var resp1 = MakePostRequest("/post", "BODY");
var resp2 = MakePostRequest("/post", "BODY");
resp1.Content.ShouldBe("Counter: 0");
resp2.Content.ShouldBe("Counter: 1");
```

### Exceptions
In the event your dynamic handler throws an exception the simulator will return a 500 status code with the exception message as the response body.

In addition, an `AggregateException` will be thrown when the simulator is stopped, containing all the exceptions that were thrown by the dynamic handlers while the simulator was running.

## Slow responses
You can test how your code handles slow server replies.

```c#
simulator.Get("/my/route").Responds("Final output").Delay(TimeSpan.FromSeconds(30));
```

## Aborted connections
You can tell the API to abort the connection (interally uses `HttpListenerResponse.Abort()`) to simulate a network disconnection. Note: if using restsharp this will cause a response timeout, so if you have a long timeout set it may appear to hang. The RestSharp status code will be `0`.
```c#
simulator.Post("/authenticate").ImmediatelyAborts();
```

## Response sequences
Sometimes you need to test that your code is able to gracefully handle a sequence of bad responses and retry successfully.
Routes can be configured to return a sequence of different responses.
    
```c#
var route = simulator.Get("/employee/1")
                    .Responds("John Smith")
                    .ThenResponds("Jane Doe")
                    .ThenResponds("Bob Jones");
```

```c#
var route = simulator.Get("/employee/1")
                    .Responds().WithCode(500)
                    .ThenResponds().WithCode(429)
                    .ThenResponds().WithCode(429)
                    .ThenResponds().WithCode(200);
```

During your tests you can reset the response index if you need to by calling `ResetCurrentResponseIndex()` on the returned route from the `ThenResponds` call.

```c#
var route = Sim.Post("/test")
        .Responds("first")
        .ThenResponds("second");
      
      MakePostRequest("/test").Content.ShouldEqual("first");
      route.ResetCurrentResponseIndex();
      // This would have returned "second" if we hadn't reset the index
      MakePostRequest("/test").Content.ShouldEqual("first");
```

At the end of the sequence the last response will be used for any future requests.

```c#
var route = Sim.Post("/test")
        .Responds("first")
        .ThenResponds("second");
      
 // First call returns "first"
 // Second call returns "second"
 // Third call returns "second" ... and so on
      
```

## Indefinitely suspend responses at runtime
You can check that your webpage correctly displays loading messages or spinners.

```c#
var route = simulator.Get("/employee/1").Responds("John Smith");
//Navigate to your page using selenium
route.Pause();
//Click "John Smith"
//Assert page shows "Loading employee details..."
route.Resume();
//Assert page shows the employee information
```

## Serialize objects
You can return objects through the simulator and they will be converted to JSON before being sent.

```c#
simulator.Put("/employee/1").Responds(new EmployeeModel());
```

### Configuring the serializer
To use the serialization methods you must provide an interface implementation of `ISerializer` in the constructor of `FluentSimulator`.

Example using [Json.Net](https://nuget.org/packages/Newtonsoft.Json) serialization:
```c#
class JsonConvertSerializer : ISerializer
{
  public string Serialize<T>(T obj) => JsonConvert.SerializeObject(obj);
  public T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json);
}
```

```c#
var simulator = new FluentSimulator("http://localhost:8080/", new JsonConvertSerializer());
```

# Asserting on requests
In addition to configuring the responses to specific URLs you can assert that the request contained all the information you're expecting.

```c#
simulator.Post("/post").Responds("OK");
//POST http://localhost/post

var requests = simulator.ReceivedRequests;
var sentAuthHeader = requests[0].Headers["Authorization"]
//Assert sentAuthHeader is correct

//Received requests is an list of ReceivedRequest which has the following data
public class ReceivedRequest
{
    public Uri Url { get; }
    public string HttpMethod { get; }
    public Encoding ContentEncoding { get; }
    public string[] AcceptTypes { get; }
    public string ContentType { get; }
    public NameValueCollection Headers { get; }
    public CookieCollection Cookies { get; }
    public NameValueCollection QueryString { get; }
    public string RawUrl { get; }
    public string UserAgent { get; }
    public string[] UserLanguage { get; }
    public string RequestBody { get; }
    public DateTime TimeOfRequest { get; }
}
```

## Deserializing requests
You can also deserialize the request using the ```BodyAs<T>``` method on the ```ReceivedRequest``` object.

```c#
simulator.Post("/employee").Responds("OK");
//POST http://localhost/post

var requests = simulator.ReceivedRequests;
var sentEmployee = requests[0].BodyAs<EmployeeModel>();

Assert.AreEqual(sentEmployee.FirstName, "John");
```

# Concurrency
The simulator is thread safe and can be used in parallel tests. A background thread is used to process the `HttpListener.BeginGetContext` calls. This has a concurrent limit that can be specified in the constructor via the optional parameter `concurrentListeners`. The default is 10.

```c#
Sim = new FluentSimulator(BaseAddress, concurrentListeners:50);
```

All methods can be used in a concurrent environment, including route configuration and `RecievedRequests` access.

# Contributing
Contributing to this project is very easy, fork the repo and start coding. Please make sure all changes are unit tested, have a look at the existing unit tests to get an idea of how it works.

