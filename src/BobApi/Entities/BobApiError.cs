using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace BobApi.Entities
{
    public class BobApiError
    {
        private BobApiError(ErrorType type, HttpResponseMessage responseMessage = null, string content = null)
        {
            Type = type;
            StatusCode = responseMessage.StatusCode;
            RequestInfo = $"{responseMessage.RequestMessage.Method}: {responseMessage.RequestMessage.RequestUri}";
            Content = content;
        }

        public ErrorType Type { get; }
        public HttpStatusCode? StatusCode { get; }
        public string RequestInfo { get; }
        public string Content { get; }

        internal static BobApiError NodeIsUnavailable() => new BobApiError(ErrorType.NodeIsUnavailable);
        internal static async Task<BobApiError> UnsuccessfulResponse(HttpResponseMessage httpResponseMessage)
            => new BobApiError(ErrorType.UnsuccessfulResponse,
                responseMessage: httpResponseMessage,
                content: await httpResponseMessage.Content.ReadAsStringAsync());

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
            var message = $"Request \"{RequestInfo}\", response [{StatusCode}]: {Content}.";
            var hint = FindHint();
            if (hint != null)
                return message + " " + hint;
            return message;
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