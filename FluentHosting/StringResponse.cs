using System.IO;
using System.Text;

namespace FluentHosting
{
	public class StringResponse : HandlerResponse
    {

        public StringResponse()
        {}

        public StringResponse(string value, Encoding encoding = null, string contentType = null) : this(value, 200, encoding, contentType)
        {
        }

        public StringResponse(string value, int code, Encoding encoding = null, string contentType = null)
        {
            Encoding = encoding ?? Encoding.UTF8;
            ContentType = contentType ?? "text/plain";
            Code = code;
            var contentBytes = Encoding.GetBytes(value);
            ContentLength = contentBytes.Length;
            Stream = new MemoryStream(contentBytes);
        }
    }
}
