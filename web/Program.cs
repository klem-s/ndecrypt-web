using System.Diagnostics;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var libraryRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("LIBRARY_ROOT") ?? "/library");
var outputRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("OUTPUT_ROOT") ?? "/output");
var ndecryptBin = Environment.GetEnvironmentVariable("NDECRYPT_BIN") ?? "/opt/ndecrypt/NDecrypt";

var romExtensions = new HashSet<string> { ".3ds", ".cci", ".cia", ".nds", ".srl" };

Directory.CreateDirectory(outputRoot);
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/files", () =>
{
    var files = new List<RomFile>();
    foreach (var path in Directory.EnumerateFiles(libraryRoot, "*", SearchOption.AllDirectories))
    {
        var ext = Path.GetExtension(path);
        if (!romExtensions.Contains(ext)) continue;

        var relative = Path.GetRelativePath(libraryRoot, path).Replace(Path.DirectorySeparatorChar, '/');
        var info = new FileInfo(path);
        var decryptedPath = Path.Combine(outputRoot, Path.GetRelativePath(libraryRoot, path));
        files.Add(new RomFile(relative, info.Length, File.Exists(decryptedPath)));
    }
    files.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
    return Results.Ok(files);
});

app.MapPost("/api/decrypt", async (DecryptRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Path))
        return Results.BadRequest(new { error = "missing path" });

    var sourcePath = ResolveWithin(libraryRoot, req.Path);
    if (sourcePath is null || !File.Exists(sourcePath))
        return Results.BadRequest(new { error = "file not found in library" });

    var destPath = Path.Combine(outputRoot, req.Path.Replace('/', Path.DirectorySeparatorChar));
    var destDir = Path.GetDirectoryName(destPath)!;
    Directory.CreateDirectory(destDir);
    File.Copy(sourcePath, destPath, overwrite: true);

    var psi = new ProcessStartInfo(ndecryptBin)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    psi.ArgumentList.Add("d");
    if (req.Force) psi.ArgumentList.Add("-f");
    psi.ArgumentList.Add(destPath);

    using var process = Process.Start(psi)!;
    var stdout = await process.StandardOutput.ReadToEndAsync();
    var stderr = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
    {
        File.Delete(destPath);
        return Results.BadRequest(new { error = "decryption failed", exitCode = process.ExitCode, stdout, stderr });
    }

    var resultInfo = new FileInfo(destPath);
    return Results.Ok(new { path = req.Path, size = resultInfo.Length, stdout, stderr });
});

app.MapGet("/api/download", (string path) =>
{
    var filePath = ResolveWithin(outputRoot, path);
    if (filePath is null || !File.Exists(filePath))
        return Results.NotFound();

    return Results.File(filePath, "application/octet-stream", Path.GetFileName(filePath));
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();

static string? ResolveWithin(string root, string relative)
{
    var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
    var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    return full.StartsWith(normalizedRoot, StringComparison.Ordinal) ? full : null;
}

record RomFile(string Path, long Size, bool Decrypted);
record DecryptRequest(string Path, [property: JsonPropertyName("force")] bool Force = false);
