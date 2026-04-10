using System;
using DuplicatiIndexer.Services.Security;
using FluentAssertions;

namespace Indexer.Tests.Security;

public class RansomwareAnalyzerTests
{
    [Fact]
    public void CalculateEntropy_EmptySpan_ReturnsZero()
    {
        var entropy = RansomwareAnalyzer.CalculateEntropy(ReadOnlySpan<byte>.Empty);
        entropy.Should().Be(0.0);
    }

    [Fact]
    public void CalculateEntropy_UniformData_ReturnsZero()
    {
        var data = new byte[100];
        Array.Fill(data, (byte)0x42);
        
        var entropy = RansomwareAnalyzer.CalculateEntropy(data);
        entropy.Should().Be(0.0);
    }

    [Fact]
    public void CalculateEntropy_HighlyRandomData_ReturnsHighEntropy()
    {
        var data = new byte[8192];
        new Random(42).NextBytes(data);
        
        var entropy = RansomwareAnalyzer.CalculateEntropy(data);
        entropy.Should().BeGreaterThan(7.9);
    }

    [Theory]
    [InlineData("txt", true)]
    [InlineData("csv", true)]
    [InlineData("md", true)]
    [InlineData("json", true)]
    [InlineData("xyz", false)]
    public void IsHeaderlessOrTextFormat_ReturnsExpectedResult(string extension, bool expected)
    {
        var result = RansomwareAnalyzer.IsHeaderlessOrTextFormat(extension);
        result.Should().Be(expected);
    }

    [Fact]
    public void VerifyMagicBytes_Pdf_MatchesSignature()
    {
        ReadOnlySpan<byte> pdfHeader = "%PDF-1.4"u8;
        var result = RansomwareAnalyzer.VerifyMagicBytes(pdfHeader, "pdf");
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyMagicBytes_Png_MatchesSignature()
    {
        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        var result = RansomwareAnalyzer.VerifyMagicBytes(pngHeader, "png");
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyMagicBytes_Mismatch_ReturnsFalse()
    {
        ReadOnlySpan<byte> fakePdf = "NOTAPDF"u8;
        var result = RansomwareAnalyzer.VerifyMagicBytes(fakePdf, "pdf");
        result.Should().BeFalse();
    }

    [Fact]
    public void SimulateMalformedEncryptedFile_BlocksProcessing()
    {
        // Arrange
        // Simulate a previously valid PDF that has been maliciously encrypted.
        // Ransomware overwrites the file with high-entropy encrypted data, destroying the magic bytes (%PDF)
        
        string extension = "pdf";
        byte[] malformedEncryptedData = new byte[8192];
        new Random(1337).NextBytes(malformedEncryptedData); // Emulate AES/ChaCha20 ciphertext
        
        var span = new ReadOnlySpan<byte>(malformedEncryptedData);

        // Act & Assert 1: Magic Bytes are destroyed
        var isValidMagicByte = RansomwareAnalyzer.VerifyMagicBytes(span, extension);
        isValidMagicByte.Should().BeFalse("Ransomware overwrote the PDF header with encrypted bytes");

        // Act & Assert 2: Entropy is critically high
        var entropy = RansomwareAnalyzer.CalculateEntropy(span);
        entropy.Should().BeGreaterThan(RansomwareAnalyzer.CRITICAL_ENTROPY_THRESHOLD, "Encrypted data has near-maximum entropy (~8.0)");
    }
}
