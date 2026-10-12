using Courier;
using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics;
using System.Reflection;

if (args.Length == 3 && args[0] == "--append")
{
    TransferHistory.Append(new("Sent", DateTimeOffset.UtcNow, "child-" + args[2] + ".bin", 1, new string('A', 64)), args[1]);
    return;
}
var root = Path.Combine(Path.GetTempPath(), "courier-receipt-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var file = Path.Combine(root, "renamed.bin"); File.WriteAllBytes(file, new byte[] { 0, 1, 2, 3 });
    var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
    var receipt = new TransferReceipt("Received", DateTimeOffset.UtcNow, "original.bin", 4, hash);
    var history = Path.Combine(root, "history.json"); TransferHistory.Append(receipt, history);
    var export = Path.Combine(root, "export.json");
    Assert(ReceiptCommand.Run(new[] { "--export-history", history, "--report", export }) == 0, "export exit");
    Assert(TransferHistory.Load(export).Single() == receipt, "export round trip");
    var verification = Path.Combine(root, "match.json");
    Assert(ReceiptCommand.Run(new[] { "--verify-receipt", export, "1", file, "--report", verification }) == 0, "match exit");
    var result = JsonSerializer.Deserialize<ReceiptVerification>(File.ReadAllText(verification))!;
    Assert(result.Matches && result.ActualLength == 4 && result.ActualSha256 == hash, "matching renamed content");
    var originalReport = File.ReadAllBytes(verification);
    Reject(() => ReceiptCommand.Run(new[] { "--verify-receipt", export, "1", file, "--report", verification }), "report overwrite");
    Assert(File.ReadAllBytes(verification).SequenceEqual(originalReport), "report unchanged");
    File.WriteAllBytes(file, new byte[] { 4, 3, 2, 1 });
    Assert(ReceiptCommand.Run(new[] { "--verify-receipt", export, "1", file, "--report", Path.Combine(root, "changed.json") }) == 3, "same-size changed bytes");
    File.WriteAllBytes(file, Array.Empty<byte>());
    Assert(ReceiptCommand.Run(new[] { "--verify-receipt", history, "1", file, "--report", Path.Combine(root, "empty.json") }) == 3, "changed size");
    foreach (var number in new[] { "0", "2", "invalid" }) Reject(() => ReceiptCommand.Run(new[] { "--verify-receipt", history, number, file, "--report", Path.Combine(root, "bad.json") }), "invalid receipt selection");
    Assert(!File.Exists(Path.Combine(root, "bad.json")), "invalid request has no report");
    Reject(() => ReceiptCommand.Run(new[] { "--export-history", history }), "argument shape");
    var invalid = Path.Combine(root, "invalid.json");
    foreach (var json in new[] { "null", "{}", "{", "[null]", JsonSerializer.Serialize(new[] { receipt with { Direction = "Other" } }), JsonSerializer.Serialize(new[] { receipt with { Name = "../private" } }), JsonSerializer.Serialize(new[] { receipt with { Sha256 = "no" } }), JsonSerializer.Serialize(new[] { receipt with { Length = -1 } }), JsonSerializer.Serialize(Enumerable.Repeat(receipt, 101)) })
    {
        File.WriteAllText(invalid, json);
        Reject(() => TransferHistory.Load(invalid), "invalid history rejected");
        Reject(() => TransferHistory.Append(receipt, invalid), "invalid history append refused");
        Assert(File.ReadAllText(invalid) == json, "invalid history preserved");
    }
    File.WriteAllText(invalid, new string(' ', TransferHistory.MaxBytes + 1));
    Reject(() => TransferHistory.Load(invalid), "oversize history");
    Reject(() => TransferHistory.Append(receipt, history, 101), "unbounded limit");
    var parallel = Path.Combine(root, "parallel.json");
    await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(() => TransferHistory.Append(receipt with { Name = "thread-" + i + ".bin" }, parallel))));
    Assert(TransferHistory.Load(parallel).Count == 20, "concurrent threads preserve receipts");
    var processes = new List<Process>();
    for (var i = 0; i < 4; i++)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        info.ArgumentList.Add("--append"); info.ArgumentList.Add(parallel); info.ArgumentList.Add(i.ToString());
        processes.Add(Process.Start(info)!);
    }
    foreach (var process in processes)
    {
        using (process) { if (!process.WaitForExit(30000)) { process.Kill(); throw new Exception("Child history writer timed out"); } Assert(process.ExitCode == 0, "child history writer"); }
    }
    Assert(TransferHistory.Load(parallel).Count == 24, "concurrent processes preserve receipts");
    Assert(!Directory.EnumerateFiles(root, "*.tmp").Any(), "no abandoned temporary history");
    Console.WriteLine("PASS: receipt export, content verification, collision preservation, malformed and bounded history, concurrent thread and process updates");
}
finally
{
    var full = Path.GetFullPath(root); var temp = Path.GetFullPath(Path.GetTempPath());
    if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("courier-receipt-tests-")) throw new Exception("Unsafe cleanup path");
    Directory.Delete(full, true);
}
static void Assert(bool value, string name) { if (!value) throw new Exception(name); }
static void Reject(Action action, string name)
{
    try { action(); } catch (Exception e) when (e is InvalidDataException or IOException or ArgumentException or JsonException or InvalidOperationException) { return; }
    throw new Exception("Accepted " + name);
}
