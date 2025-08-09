using System.Security.Cryptography;
using System.Text;

namespace ErnestAi.Tools
{
    public class Md5HashProvider : IContentHashProvider
    {
        public string ComputeMd5(string input)
        {
            using var md5 = MD5.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = md5.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
