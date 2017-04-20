# FluentSimulator
FluentSimulator is a .Net HTTP API simulator library that is extremely versitile and unassuming. 

There are 2 main use cases for FluentSimulator.

- Simulating a 3rd party API to unit test your code that interacts with that API
- Mocking out REST or other APIs during automated browser based testing using something like selenium - useful for light weight single page application testing.

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

# Features
The simulator provides some useful functions and can be very useful in testing outputs that aren't easy to recreate using real endpoints.


## Serialise objects
You can return objects through the simulator and they will be converted to JSON before being sent.

```c#
    simulator.Put("/employee/1").Responds(new EmployeeModel());
```

### Configuring the serializer
Internally the simulator uses the Newtonsoft [Json.NET](https://github.com/JamesNK/Newtonsoft.Json) library you can pass in your own serializer settings.

```c#
    var simulator = new FluentSimulator("http://localhost:8080/", new JsonSerialiserSettings());
```

## Status codes
Want to see how your code handles 500 server errors, or 404s?

```c#
    simulator.Get("/my/route").Responds().WithCode(500);
    simulator.Get("/employee/44").Responds().WithCode(404);
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

# Contributing
Contributing to this project is very easy, fork the repo and start coding. Please make sure all changes are unit tested, have a look at the existing unit tests to get an idea of how it works.

