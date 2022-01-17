using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace BobApi.Entities
{
    public class BobApiError
    {
        private BobApiError(ErrorType type, HttpStatusCode? statusCode = null, string requestInfo = null,
            string content = null)
        {
            Type = type;
            StatusCode = statusCode;
            RequestInfo = requestInfo;
            Content = content;
        }

        public ErrorType Type { get; }
        public HttpStatusCode? StatusCode { get; }
        public string RequestInfo { get; }
        public string Content { get; }

        internal static BobApiError NodeIsUnavailable() => new BobApiError(ErrorType.NodeIsUnavailable, null);
        internal static async Task<BobApiError> UnsuccessfulResponse(HttpResponseMessage httpResponseMessage)
            => new BobApiError(ErrorType.UnsuccessfulResponse, httpResponseMessage.StatusCode,
                $"{httpResponseMessage.RequestMessage.Method}: {httpResponseMessage.RequestMessage.RequestUri}",
                await httpResponseMessage.Content.ReadAsStringAsync());

        public override string ToString()
        {
            return Type switch
            {
                ErrorType.UnsuccessfulResponse => $"Request \"{RequestInfo}\", response [{StatusCode}]: {Content}",
                ErrorType.NodeIsUnavailable => $"Request \"{RequestInfo}\" failed, node is unavailable",
                _ => Type.ToString()
            };
        }
    }

    public enum ErrorType
    {
        NodeIsUnavailable,
        UnsuccessfulResponse
    }
}