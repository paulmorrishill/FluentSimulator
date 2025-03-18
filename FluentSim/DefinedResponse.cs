using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

namespace FluentSim;

[DebuggerDisplay("{Description}")]
internal class DefinedResponse
{
    public string Output = "";
    private string Description { get; set; }
    public void AddDescriptionPart(string part) => Description += " " + part;
    public byte[] BinaryOutput = null;
    public bool ShouldImmediatelyDisconnect = false;
    public List<Action<HttpListenerContext>> ResponseModifiers = new List<Action<HttpListenerContext>>();
    public Func<ReceivedRequest, string> HandlerFunction { get; set; }

    internal string GetBody(ReceivedRequest request)
    {
        if (HandlerFunction != null)
            return HandlerFunction(request);
        return Output;
    }
        
    internal void RunContextModifiers(HttpListenerContext context)
    {
        foreach (var responseModifier in ResponseModifiers)
            responseModifier(context);
    }
}