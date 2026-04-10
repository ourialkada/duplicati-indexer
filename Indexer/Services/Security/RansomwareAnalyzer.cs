using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DuplicatiIndexer.Services.Security;

/// <summary>
/// High-performance, zero-allocation Ransomware Analysis module designed for 
/// local stream interception.
/// </summary>
public static class RansomwareAnalyzer
{
    // Entropy threshold indicating strong encryption/compression.
    public const double CRITICAL_ENTROPY_THRESHOLD = 7.9d;

    /// <summary>
    /// Highly optimized Shannon Entropy calculator. 
    /// Avoids GC allocations completely and performs calculation in an optimized O(N) pass.
    /// Recommended to pass 8KB-64KB chunks rather than gigabytes for microsecond evaluation.
    /// </summary>
    /// <param name="data">The byte sequence to analyze</param>
    /// <returns>Shannon Entropy value between 0.0 and 8.0</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateEntropy(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return 0.0d;

        // Stack allocate the counting array (1024 bytes) - absolutely zero GC pressure
        Span<int> byteCounts = stackalloc int[256];

        // Bypass standard array/span bounds checking for ultra-fast loop iteration 
        ref byte searchSpace = ref MemoryMarshal.GetReference(data);
        int length = data.Length;
        
        for (int i = 0; i < length; i++)
        {
            byteCounts[Unsafe.Add(ref searchSpace, i)]++;
        }

        double entropySum = 0d;

        // Compute entropy using algebraic simplification:
        // H = SUM(- (c/N) * Log2(c/N)) 
        //   = Log2(N) - (SUM(c * Log2(c)) / N)
        // This pulls the division out of the loop, reducing CPU latency massively.
        for (int i = 0; i < 256; i++)
        {
            int count = byteCounts[i];
            if (count > 0)
            {
                entropySum += count * Math.Log2(count);
            }
        }

        return Math.Log2(length) - (entropySum / length);
    }

    /// <summary>
    /// Verifies standard Magic Bytes signature against the expected file extension.
    /// Uses UTF-8 u8 literals and C# 12+ collection expressions for raw-memory mapped bytes.
    /// </summary>
    /// <param name="header">The first 4-8 bytes of the file stream</param>
    /// <param name="extension">File extension (without the dot, e.g. "pdf")</param>
    /// <returns>True if signature matches, false if mismatched (potential encrypted rename)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool VerifyMagicBytes(ReadOnlySpan<byte> header, ReadOnlySpan<char> extension)
    {
        if (header.IsEmpty || extension.IsEmpty) return false;

        // Use pattern matching and ReadOnlySpan StartsWith for zero-allocation comparisons
        if (extension.Equals("pdf", StringComparison.OrdinalIgnoreCase))
        {
             // PDF: %PDF
            return header.StartsWith("%PDF"u8);
        }
        else if (extension.Equals("png", StringComparison.OrdinalIgnoreCase))
        {
            // PNG: 89 50 4E 47 0D 0A 1A 0A
            // Leveraging C# 12 inline collection limits allocation to static read-only memory
            return header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        }
        else if (extension.Equals("jpg", StringComparison.OrdinalIgnoreCase) || 
                 extension.Equals("jpeg", StringComparison.OrdinalIgnoreCase))
        {
            // JPEG: FF D8 FF
            return header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]);
        }
        else if (extension.Equals("zip", StringComparison.OrdinalIgnoreCase) ||
                 extension.Equals("docx", StringComparison.OrdinalIgnoreCase) ||
                 extension.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
        {
            // PKZIP (Office Open XML formats): PK.. (50 4B 03 04)
            return header.StartsWith("PK\x03\x04"u8); 
        }

        // Failsafe for known headerless formats (or handle rigorously depending on posture)
        return IsHeaderlessOrTextFormat(extension);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHeaderlessOrTextFormat(ReadOnlySpan<char> extension)
    {
        return extension.Equals("txt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals("csv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals("json", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals("md", StringComparison.OrdinalIgnoreCase);
    }
}
