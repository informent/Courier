using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Courier;

public sealed record ReceiptExport(int SchemaVersion, DateTimeOffset ExportedAt, IReadOnlyList<TransferReceipt> Receipts);
public sealed record ReceiptVerification(int SchemaVersion, int ReceiptNumber, TransferReceipt Receipt, long ActualLength, string ActualSha256, bool Matches);

public static class ReceiptCommand
{
    public static bool IsCommand(string[] args) => args.Length > 0 && args[0] is "--export-history" or "--verify-receipt";

    public static int Run(string[] args)
    {
        if (!IsCommand(args) || args.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Invalid receipt command.");
        object report; var code = 0; string destination;
        if (args[0] == "--export-history" && args.Length == 4 && args[2] == "--report")
        {
            report = new ReceiptExport(1, DateTimeOffset.UtcNow, TransferHistory.Load(HistoryPath(args[1])));
            destination = args[3];
        }
        else if (args[0] == "--verify-receipt" && args.Length == 6 && args[4] == "--report" && int.TryParse(args[2], out var number))
        {
            var receipts = TransferHistory.Load(HistoryPath(args[1]));
            if (number < 1 || number > receipts.Count) throw new ArgumentOutOfRangeException(nameof(number), "Choose a receipt number from the report.");
            var receipt = receipts[number - 1];
            using var stream = new FileStream(args[3], FileMode.Open, FileAccess.Read, FileShare.Read);
            var length = stream.Length; var hash = Convert.ToHexString(SHA256.HashData(stream));
            var matches = length == receipt.Length && hash.Equals(receipt.Sha256, StringComparison.OrdinalIgnoreCase);
            report = new ReceiptVerification(1, number, receipt, length, hash, matches);
            destination = args[5]; code = matches ? 0 : 3;
        }
        else throw new ArgumentException("Use --export-history HISTORY --report REPORT or --verify-receipt HISTORY NUMBER FILE --report REPORT.");
        WriteReport(destination, report);
        return code;
    }

    private static string HistoryPath(string value) => value == "default" ? TransferHistory.DefaultPath : value;

    private static void WriteReport(string destination, object report)
    {
        var path = Path.GetFullPath(destination);
        if (File.Exists(path) || Directory.Exists(path)) throw new IOException("The report destination already exists.");
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, ".courier-report-" + Guid.NewGuid().ToString("N"));
        try
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(report, report.GetType(), new JsonSerializerOptions { WriteIndented = true });
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(data); stream.Flush(true); }
            File.Move(temporary, path, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
