using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Elara.Context.Contracts;
using Elara.Core.Paths;

namespace Elara.Context
{
    /// <summary>
    /// Append-only, per-message JSON file store with AES-256-GCM envelope encryption.
    /// Files are written under CacheRoot/Conversation by default.
    /// </summary>
    public sealed class FileConversationStore : IConversationStore
    {
        private readonly string _root;
        private readonly byte[] _key; // 32 bytes for AES-256
        private int _seq;

        private sealed class Envelope
        {
            public required string Alg { get; init; } // e.g., AES-256-GCM
            public required string Iv { get; init; } // base64(12 bytes)
            public required string Ciphertext { get; init; }
            public required string Tag { get; init; } // base64(16 bytes)
        }

        /// <summary>
        /// Create store using the provided storage root and encryption key string.
        /// The key string will be SHA-256 hashed to produce a 32-byte key.
        /// </summary>
        public FileConversationStore(string? storageRoot, string encryptionKey)
        {
            if (string.IsNullOrWhiteSpace(encryptionKey))
                throw new ArgumentException("Encryption key must be provided", nameof(encryptionKey));

            using var sha = SHA256.Create();
            _key = sha.ComputeHash(Encoding.UTF8.GetBytes(encryptionKey));

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

            // Encrypt
            var nonce = RandomNumberGenerator.GetBytes(12);
            var plaintext = Encoding.UTF8.GetBytes(json);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];
            using (var aes = new AesGcm(_key, 16))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag);
            }

            var env = new Envelope
            {
                Alg = "AES-256-GCM",
                Iv = Convert.ToBase64String(nonce),
                Ciphertext = Convert.ToBase64String(ciphertext),
                Tag = Convert.ToBase64String(tag)
            };

            var envJson = JsonSerializer.Serialize(env, new JsonSerializerOptions { WriteIndented = false });

            var ts = message.TimestampUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfff'Z'");
            var role = message.Role.ToString().ToLowerInvariant();
            var seq = Interlocked.Increment(ref _seq).ToString("D4");
            var path = Path.Combine(_root, $"{ts}_{seq}_{role}.json");

            await File.WriteAllTextAsync(path, envJson, ct).ConfigureAwait(false);
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
                var envJson = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                Envelope? env;
                try
                {
                    env = JsonSerializer.Deserialize<Envelope>(envJson);
                }
                catch
                {
                    continue; // skip corrupt
                }
                if (env is null) continue;

                try
                {
                    var nonce = Convert.FromBase64String(env.Iv);
                    var ciphertext = Convert.FromBase64String(env.Ciphertext);
                    var tag = Convert.FromBase64String(env.Tag);

                    var plaintext = new byte[ciphertext.Length];
                    using var aes = new AesGcm(_key, 16);
                    aes.Decrypt(nonce, ciphertext, tag, plaintext);
                    var json = Encoding.UTF8.GetString(plaintext);
                    var msg = JsonSerializer.Deserialize<ChatMessage>(json);
                    if (msg != null) list.Add(msg);
                }
                catch
                {
                    // skip on decrypt error
                }
            }
            return list;
        }
    }
}
