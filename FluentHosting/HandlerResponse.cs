using System.IO;
using System.Text;

namespace FluentHosting
{
	public class HandlerResponse : IHandlerResponse
    {
        public Stream Stream { get; set; }
        public string ContentType { get; set; }
        public Encoding Encoding { get; set; }
        public int Code { get; set; }
        public long ContentLength { get; set; }
    }
}
