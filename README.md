# FluentSimulator
FluentSimulator is a .Net HTTP API simulator library that is extremely versitile and unassuming. 

There are 2 main use cases for FluentSimulator.

- Simulating a 3rd party API to unit test your code that interacts with that API
- Mocking out REST or other APIs during automated browser based testing using something like selenium - useful for light weight single page application testing.

[![Build status](https://ci.appveyor.com/api/projects/status/wi118asgtpeqg2ed?svg=true)](https://ci.appveyor.com/project/paulmorrishill/fluentsimulator)
[![AppVeyor tests](https://img.shields.io/appveyor/tests/paulmorrishill/fluentsimulator.svg)](https://ci.appveyor.com/project/paulmorrishill/fluentsimulator/build/tests)
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

## Non matching requests
Any request that does not match any configured responses will return a *501 Not Implemented* status code and an empty response body.

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
Internally the simulator uses the Newtonsoft [Json.NET](https://github.com/JamesNK/Newtonsoft.Json) library you can pass in your own serializer settings.

```c#
    var simulator = new FluentSimulator("http://localhost:8080/", new JsonSerialiserSettings());
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

# Contributing
Contributing to this project is very easy, fork the repo and start coding. Please make sure all changes are unit tested, have a look at the existing unit tests to get an idea of how it works.

