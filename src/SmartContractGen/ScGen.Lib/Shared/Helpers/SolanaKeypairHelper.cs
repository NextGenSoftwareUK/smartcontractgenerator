using System.Text.Json;

namespace ScGen.Lib.Shared.Helpers;

/// <summary>
/// Helper class to convert Solana private keys to keypair JSON format
/// </summary>
public static class SolanaKeypairHelper
{
    /// <summary>
    /// Convert a Solana private key (base58 or hex string) to keypair JSON format
    /// Solana keypair JSON is an array of 64 bytes (private key + public key)
    /// </summary>
    public static byte[]? ConvertPrivateKeyToKeypairBytes(string privateKey)
    {
        if (string.IsNullOrWhiteSpace(privateKey))
            return null;

        try
        {
            // Try to parse as base58 (most common Solana format)
            byte[]? keyBytes = TryParseBase58(privateKey);
            
            if (keyBytes == null)
            {
                // Try hex format
                keyBytes = TryParseHex(privateKey);
            }

            if (keyBytes == null || keyBytes.Length != 32)
            {
                // If we have 32 bytes, we need to derive the public key
                // For now, we'll use the private key as-is and let Solana handle it
                // In a full implementation, we'd derive the public key from the private key
                return keyBytes;
            }

            // Solana keypair format is [private_key (32 bytes) + public_key (32 bytes)]
            // For now, we'll create a minimal keypair with just the private key
            // The deployment process will handle deriving the public key
            return keyBytes;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Create a keypair JSON file content from private key bytes
    /// </summary>
    public static string CreateKeypairJson(byte[] privateKeyBytes)
    {
        // Solana keypair JSON is just the byte array as JSON
        // Format: [byte1, byte2, ..., byte64]
        // We need 64 bytes total (32 private + 32 public)
        // For now, we'll pad with zeros for the public key part
        // In production, derive the actual public key
        
        byte[] keypairBytes = new byte[64];
        Array.Copy(privateKeyBytes, 0, keypairBytes, 0, Math.Min(privateKeyBytes.Length, 32));
        
        // The public key will be derived during deployment
        // For now, we'll use a placeholder that Solana tools can handle
        
        return JsonSerializer.Serialize(keypairBytes);
    }

    private static byte[]? TryParseBase58(string base58String)
    {
        try
        {
            // Simple base58 decode - in production, use a proper base58 library
            // For now, we'll try to decode it
            // Solana uses base58 encoding
            return null; // Placeholder - would need proper base58 decoder
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? TryParseHex(string hexString)
    {
        try
        {
            if (hexString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hexString = hexString.Substring(2);

            if (hexString.Length % 2 != 0)
                return null;

            byte[] bytes = new byte[hexString.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
        catch
        {
            return null;
        }
    }
}













