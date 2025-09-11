using System;
using System.Security.Cryptography;
using System.Text;

namespace Engine.Pipelines.Core;

public static class ContentHashGenerator
{
    public static string ComputeHash(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        return ComputeHash(bytes);
    }

    public static string ComputeHash(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        byte[] hash = SHA256.HashData(bytes);
        // Use first 12 hex chars (48 bits) for brevity + low collision risk
        return ConvertToHex(hash, 12);
    }

    private static string ConvertToHex(byte[] bytes, int length)
    {
        const string hex = "0123456789abcdef";
        int take = Math.Clamp(length, 1, bytes.Length * 2);
        char[] chars = new char[take];
        int charIndex = 0;
        for (int i = 0; i < bytes.Length && charIndex < take; i++)
        {
            byte b = bytes[i];
            chars[charIndex++] = hex[b >> 4];
            if (charIndex >= take)
            {
                break;
            }
            chars[charIndex++] = hex[b & 0xF];
        }

        return new string(chars);
    }
}

