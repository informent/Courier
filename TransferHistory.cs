using System.Text.Json;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Courier;

public static class TransferHistory
{
    public const int MaxEntries = 100;
    public const int MaxBytes = 1024 * 1024;
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenShare", "transfer-history.json");

    public static IReadOnlyList<TransferReceipt> Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return [];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (stream.Length > MaxBytes) throw new InvalidDataException("Receipt history exceeds its size limit.");
        var buffer = new byte[MaxBytes + 1]; var count = 0;
        while (count < buffer.Length)
        {
            var read = stream.Read(buffer, count, buffer.Length - count);
            if (read == 0) break;
            count += read;
        }
        if (count > MaxBytes) throw new InvalidDataException("Receipt history exceeds its size limit.");
        using var document = JsonDocument.Parse(buffer.AsMemory(0, count));
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (!root.TryGetProperty("SchemaVersion", out var schema) || schema.GetInt32() != 1 || !root.TryGetProperty("Receipts", out root))
                throw new InvalidDataException("Unsupported receipt export.");
        }
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() > MaxEntries) throw new InvalidDataException("Receipt history must contain at most 100 receipts.");
        var entries = root.Deserialize<TransferReceipt[]>() ?? throw new InvalidDataException("Receipt history is invalid.");
        foreach (var receipt in entries) Validate(receipt);
        return entries;
    }

    public static void Append(TransferReceipt receipt, string? path = null, int limit = MaxEntries)
    {
        Validate(receipt);
        if (limit is < 1 or > MaxEntries) throw new ArgumentOutOfRangeException(nameof(limit));
        path = Path.GetFullPath(path ?? DefaultPath);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())));
        using var mutex = new Mutex(false, "Local\\Courier-history-" + key);
        var acquired = false;
        try
        {
            try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new IOException("Receipt history is busy.");
            var entries = Load(path).Append(receipt).TakeLast(limit).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var data = JsonSerializer.SerializeToUtf8Bytes(entries, new JsonSerializerOptions { WriteIndented = true });
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(data); stream.Flush(true); }
                File.Move(temporary, path, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        finally { if (acquired) mutex.ReleaseMutex(); }
    }

    public static void Validate(TransferReceipt receipt)
    {
        if (receipt is null || receipt.Direction is not ("Sent" or "Received") || receipt.CompletedAt == default || receipt.Length < 0 ||
            string.IsNullOrWhiteSpace(receipt.Name) || receipt.Name.Length > 255 || receipt.Name != Path.GetFileName(receipt.Name) ||
            receipt.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || receipt.Name.EndsWith('.') || receipt.Name.EndsWith(' ') ||
            receipt.Sha256 is null || receipt.Sha256.Length != 64 || !receipt.Sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("Receipt metadata is invalid.");
    }
}
