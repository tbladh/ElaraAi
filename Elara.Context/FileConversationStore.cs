using System.Text.Json;
using Elara.Context.Contracts;
using Elara.Core.Paths;
using Elara.Context.Extensions;
using System.Text.Encodings.Web;

namespace Elara.Context
{
    /// <summary>
    /// Append-only, per-message JSON file store. When an encryption key is provided, messages are stored
    /// inside an AES-256-GCM envelope. If the key is null/whitespace, messages are stored as plain JSON.
    /// Files are written under CacheRoot/Conversation by default.
    /// </summary>
    public sealed class FileConversationStore : IConversationStore
    {
        private readonly string _root;
        private readonly byte[]? _key; // null => encryption disabled
        private int _seq;
        private static readonly JsonSerializerOptions EnvelopeJsonOptions = new()
        {
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
  
        /// <summary>
        /// Create store using the provided storage root and optional encryption key string.
        /// If a non-empty key is provided, it will be SHA-256 hashed to produce a 32-byte key.
        /// </summary>
        public FileConversationStore(string? storageRoot, string? encryptionKey)
        {
            if (!string.IsNullOrWhiteSpace(encryptionKey))
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                _key = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(encryptionKey));
            }
            else
            {
                _key = null; // encryption disabled
            }

            var root = storageRoot;
            if (string.IsNullOrWhiteSpace(root))
            {
                var cache = ModelPaths.GetCacheRoot();
                root = Path.Combine(cache, "Conversation");
            }
            Directory.CreateDirectory(root!);
            _root = root!;
            _seq = 0;
        }

        

        public async Task AppendMessageAsync(ChatMessage message, CancellationToken ct = default)
        {
            // Serialize message first
            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions { WriteIndented = false });

            string toWrite;
            // Always write envelopes: AES-256-GCM if key present; otherwise PLAINTEXT
            if (_key is not null)
            {
                var encEnv = json.ToEncryptedEnvelope(_key); // Envelope<string>
                toWrite = JsonSerializer.Serialize(encEnv, EnvelopeJsonOptions);
            }
            else
            {
                var plainEnv = json.ToPlaintextEnvelope(); // Envelope<JsonElement>
                toWrite = JsonSerializer.Serialize(plainEnv, EnvelopeJsonOptions);
            }

            var ts = message.TimestampUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfff'Z'");
            var role = message.Role.ToString().ToLowerInvariant();
            var seq = Interlocked.Increment(ref _seq).ToString("D4");
            var path = Path.Combine(_root, $"{ts}_{seq}_{role}.json");

            await File.WriteAllTextAsync(path, toWrite, ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<ChatMessage>> ReadTailAsync(int n, CancellationToken ct = default)
        {
            if (n <= 0) return Array.Empty<ChatMessage>();

            var files = Directory.GetFiles(_root, "*.json")
                .Select(fp => new { Full = fp, Name = Path.GetFileName(fp) })
                .OrderByDescending(x => x.Name)
                .Take(n)
                .OrderBy(x => x.Name)
                .Select(x => x.Full)
                .ToArray();

            var list = new List<ChatMessage>(files.Length);
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                var text = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);

                // Prefer envelope parsing; fallback to legacy plain JSON
                // 1) Try AES envelope (Envelope<string>)
                if (EncryptionExtensions.TryDeserializeEnvelope(text, out var aesEnv) && aesEnv is not null &&
                    string.Equals(aesEnv.Alg, "AES-256-GCM", StringComparison.OrdinalIgnoreCase))
                {
                    if (_key is null)
                    {
                        // Encrypted content present but no key available; skip
                        continue;
                    }
                    if (aesEnv.TryDecryptEnvelope(_key, out var plaintextJson) && plaintextJson is not null)
                    {
                        var msg = JsonSerializer.Deserialize<ChatMessage>(plaintextJson);
                        if (msg != null) list.Add(msg);
                    }
                    // on failure, skip
                }
                // 2) Try PLAINTEXT envelope (Envelope<JsonElement>)
                else if (EncryptionExtensions.TryDeserializeEnvelopeElement(text, out var plainEnv) && plainEnv is not null &&
                         string.Equals(plainEnv.Alg, "PLAINTEXT", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var msg = JsonSerializer.Deserialize<ChatMessage>(plainEnv.Content.GetRawText());
                        if (msg != null) list.Add(msg);
                    }
                    catch
                    {
                        // skip corrupt
                    }
                }
                else
                {
                    // Legacy plain JSON ChatMessage (pre-envelope)
                    try
                    {
                        var msg = JsonSerializer.Deserialize<ChatMessage>(text);
                        if (msg != null) list.Add(msg);
                    }
                    catch
                    {
                        // skip corrupt
                    }
                }
            }
            return list;
        }
    }
}
