using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace FluentSim;

public class UnexpectedRequestException : ApplicationException
{
    private List<ReceivedRequest> UnexpectedRequests;

    public UnexpectedRequestException(List<ReceivedRequest> requests) : base(CreateMessage(requests))
    {
        UnexpectedRequests = requests;
    }

    private static string CreateMessage(List<ReceivedRequest> requests)
    {
        var message = new StringBuilder();
        message.AppendLine("One or more unexpected requests were received:");

        foreach (var request in requests)
        {
            message.AppendLine("--------------------------------------------------");
            message.AppendLine($"Time of Request: {request.TimeOfRequest}");
            message.AppendLine($"URL: {request.Url}");
            message.AppendLine($"HTTP Method: {request.HttpMethod}");
            message.AppendLine($"Content Encoding: {request.ContentEncoding?.EncodingName}");
            message.AppendLine($"Content Type: {request.ContentType}");
            message.AppendLine($"Accept Types: {string.Join(", ", request.AcceptTypes ?? Array.Empty<string>())}");
            message.AppendLine($"User Agent: {request.UserAgent}");
            message.AppendLine(
                $"User Languages: {string.Join(", ", request.UserLanguage ?? Array.Empty<string>())}");
            message.AppendLine($"Raw URL: {request.RawUrl}");
            message.AppendLine("Headers:");

            if (request.Headers != null)
            {
                foreach (var key in request.Headers.AllKeys)
                {
                    message.AppendLine($"  {key}: {request.Headers[key]}");
                }
            }

            message.AppendLine("Query String Parameters:");
            if (request.QueryString != null)
            {
                foreach (var key in request.QueryString.AllKeys)
                {
                    message.AppendLine($"  {key}: {request.QueryString[key]}");
                }
            }

            message.AppendLine("Cookies:");
            if (request.Cookies != null)
            {
                foreach (Cookie cookie in request.Cookies)
                {
                    message.AppendLine($"  {cookie.Name} = {cookie.Value}");
                }
            }

            message.AppendLine("Request Body:");
            message.AppendLine(request.RequestBody ?? "[No Body]");
        }

        return message.ToString();
    }
}