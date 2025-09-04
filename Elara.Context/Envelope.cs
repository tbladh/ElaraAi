namespace Elara.Context
{
    public sealed class Envelope<T>
    {
        public required string Alg { get; init; } // e.g., AES-256-GCM or PLAINTEXT
        public string? Iv { get; init; } // base64(12 bytes) for AES; null for PLAINTEXT
        public required T Content { get; init; }
        public string? Tag { get; init; } // base64(16 bytes) for AES; null for PLAINTEXT
    }
}
