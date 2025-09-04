using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Elara.Context.Extensions
{
    public static class EncryptionExtensions
    {
        // Produce PLAINTEXT envelope from a UTF8 JSON payload, embedding the JSON object directly
        public static Envelope<JsonElement> ToPlaintextEnvelope(this string plaintextJson)
        {
            using var doc = JsonDocument.Parse(plaintextJson);
            return new Envelope<JsonElement>
            {
                Alg = "PLAINTEXT",
                Iv = null,
                Content = doc.RootElement.Clone(),
                Tag = null
            };
        }

        // Produce AES-256-GCM envelope from a UTF8 JSON payload
        public static Envelope<string> ToEncryptedEnvelope(this string plaintextJson, byte[] key)
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var plaintext = Encoding.UTF8.GetBytes(plaintextJson);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];
            using (var aes = new AesGcm(key, 16))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag);
            }

            return new Envelope<string>
            {
                Alg = "AES-256-GCM",
                Iv = Convert.ToBase64String(nonce),
                Content = Convert.ToBase64String(ciphertext),
                Tag = Convert.ToBase64String(tag)
            };
        }

        // Attempt to decrypt an envelope into a UTF8 JSON payload
        public static bool TryDecryptEnvelope(this Envelope<string> env, byte[] key, out string? plaintextJson)
        {
            plaintextJson = null;
            if (!string.Equals(env.Alg, "AES-256-GCM", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                if (env.Iv is null || env.Tag is null)
                    return false;
                var nonce = Convert.FromBase64String(env.Iv);
                var ciphertext = Convert.FromBase64String(env.Content);
                var tag = Convert.FromBase64String(env.Tag);

                var plaintext = new byte[ciphertext.Length];
                using var aes = new AesGcm(key, 16);
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
                plaintextJson = Encoding.UTF8.GetString(plaintext);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Helper to detect whether text appears to be an Envelope JSON
        public static bool TryDeserializeEnvelope(string json, out Envelope<string>? env)
        {
            env = null;
            try
            {
                env = JsonSerializer.Deserialize<Envelope<string>>(json);
                // Require at least Alg and Content to consider it an envelope
                if (env is null) return false;
                return !string.IsNullOrWhiteSpace(env.Alg) && env.Content is not null;
            }
            catch
            {
                return false;
            }
        }

        // Helper to detect/parse an envelope whose Content is a JSON object (for PLAINTEXT)
        public static bool TryDeserializeEnvelopeElement(string json, out Envelope<JsonElement>? env)
        {
            env = null;
            try
            {
                env = JsonSerializer.Deserialize<Envelope<JsonElement>>(json);
                if (env is null) return false;
                return !string.IsNullOrWhiteSpace(env.Alg);
            }
            catch
            {
                return false;
            }
        }
    }
}
