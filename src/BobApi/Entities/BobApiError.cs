using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BobApi.Entities
{
    public class BobApiError
    {
        private BobApiError(
            ErrorType type,
            HttpStatusCode? httpStatusCode = null,
            HttpMethod requestMethod = null,
            Uri requestUri = null,
            Uri baseUri = null,
            string content = null
        )
        {
            Type = type;
            StatusCode = httpStatusCode;
            RequestMethod = requestMethod;
            RequestUri1 = requestUri;
            BaseUri = baseUri;
            Content = content;
        }

        public ErrorType Type { get; }
        public HttpStatusCode? StatusCode { get; }
        public HttpMethod RequestMethod { get; }
        public Uri RequestUri1 { get; }
        public Uri BaseUri { get; }
        public string RequestUri { get; }
        public string Content { get; }

        internal static BobApiError NodeIsUnavailable() =>
            new BobApiError(ErrorType.NodeIsUnavailable);

        internal static BobApiError UnsuccessfulResponse(
            HttpMethod requestMethod,
            HttpStatusCode? statusCode,
            Uri baseUri,
            Uri requestUri,
            string content
        ) =>
            new BobApiError(
                ErrorType.UnsuccessfulResponse,
                requestMethod: requestMethod,
                requestUri: requestUri,
                content: content,
                baseUri: baseUri,
                httpStatusCode: statusCode
            );

        public override string ToString()
        {
            return Type switch
            {
                ErrorType.UnsuccessfulResponse => GetUnsuccessfulResponseMessage(),
                ErrorType.NodeIsUnavailable => "Node is unavailable.",
                _ => Type.ToString()
            };
        }

        private string GetUnsuccessfulResponseMessage()
        {
            var result = new StringBuilder(
                $"Client {BaseUri}, request \"{RequestMethod}: {RequestUri}\", response [{StatusCode}]"
            );

            if (Content == null || Content.Contains('\n'))
                result.Append('.');
            else
                result.Append($": {Content}.");

            var hint = FindHint();
            if (hint != null)
                result.Append($" {hint}");
            return result.ToString();
        }

        private string FindHint()
        {
            if (StatusCode == HttpStatusCode.NotFound)
                return "Probably bobd has incompatible version.";
            return null;
        }
    }

    public enum ErrorType
    {
        NodeIsUnavailable,
        UnsuccessfulResponse
    }
}
