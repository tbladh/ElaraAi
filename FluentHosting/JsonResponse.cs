using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FluentHosting
{
    public class JsonResponse<T>: HandlerResponse
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public JsonResponse(T value, Encoding encoding = null) : this(value, 200, encoding)
        {
        }

        public JsonResponse(T value, int code, Encoding encoding = null)
        {
            Encoding = encoding ?? Encoding.UTF8;
            ContentType = "application/json; charset=utf-8";
            Code = code;
            var json = JsonSerializer.Serialize(value, JsonOptions);
            var contentBytes = Encoding.GetBytes(json);
            ContentLength = contentBytes.Length;
            Stream = new MemoryStream(contentBytes);
        }
    }
}
