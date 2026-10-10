using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace TibiaScarabEye.Tests;

// installer/obs-script.ps1 edita o JSON da colecao de cenas do OBS; aqui ele roda contra uma configuracao falsa.
public sealed class ObsScriptRegistrationTests : IDisposable
{
    private const string Lua = @"C:\Users\x\AppData\Local\Programs\Tibia Scarab Eye\obs\TibiaScarabEye.lua";
    private const string LuaJson = "C:/Users/x/AppData/Local/Programs/Tibia Scarab Eye/obs/TibiaScarabEye.lua";

    private readonly string config = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "tibiascarabeye-obs-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(config, recursive: true);

    private string Collection(string name = "Sem_nome.json") => Path.Combine(config, "basic", "scenes", name);

    private void Write(string json, string name = "Sem_nome.json", bool active = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Collection(name)));
        File.WriteAllText(Collection(name), json);
        if (active) File.WriteAllText(Path.Combine(config, "user.ini"), "[Basic]\nSceneCollection=x\nSceneCollectionFile=" + name + "\n");
    }

    private static string ScriptFile()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string file = Path.Combine(dir.FullName, "installer", "obs-script.ps1");
            if (File.Exists(file)) return file;
        }
        throw new FileNotFoundException("installer/obs-script.ps1");
    }

    private int Run(string action)
    {
        Assert.SkipWhen(Process.GetProcessesByName("obs64").Length > 0, "o OBS esta aberto: o script se recusa a editar a configuracao");
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        foreach (string arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ScriptFile(), "-Action", action, "-Script", Lua, "-ObsConfig", config })
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start);
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }

    private string[] Scripts(string name = "Sem_nome.json")
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Collection(name)));
        if (!doc.RootElement.TryGetProperty("modules", out var modules) || !modules.TryGetProperty("scripts-tool", out var list)) return Array.Empty<string>();
        return list.EnumerateArray().Select(entry => entry.GetProperty("path").GetString()).ToArray();
    }

    [Fact]
    public void Register_AddsTheScriptToAnExistingList()
    {
        Write("{\n  \"name\": \"c\",\n  \"modules\": {\n    \"scripts-tool\": [\n      {\"path\": \"C:/outro.lua\", \"settings\": {}}\n    ],\n    \"output-timer\": {\"a\": 1}\n  }\n}");
        Assert.Equal(0, Run("Register"));
        Assert.Equal(new[] { LuaJson, "C:/outro.lua" }, Scripts());
    }

    [Fact]
    public void Register_AddsAListToModulesThatHasNone()
    {
        Write("{\"name\": \"c\", \"modules\": {\"output-timer\": {\"a\": 1}}}");
        Assert.Equal(0, Run("Register"));
        Assert.Equal(new[] { LuaJson }, Scripts());
    }

    [Fact]
    public void Register_AddsAnEmptyModulesAndAnEmptyList()
    {
        Write("{\"name\": \"c\", \"modules\": {}}");
        Assert.Equal(0, Run("Register"));
        Assert.Equal(new[] { LuaJson }, Scripts());

        Write("{\"name\": \"c\", \"modules\": {\"scripts-tool\": []}}");
        Assert.Equal(0, Run("Register"));
        Assert.Equal(new[] { LuaJson }, Scripts());
    }

    [Fact]
    public void Register_CreatesModulesWhenTheCollectionHasNoneAndKeepsABackup()
    {
        string original = "{\"name\": \"c\", \"sources\": []}";
        Write(original);
        Assert.Equal(0, Run("Register"));
        Assert.Equal(new[] { LuaJson }, Scripts());
        Assert.Equal(original, File.ReadAllText(Collection() + ".scarab-eye.bak"));
    }

    [Fact]
    public void Register_MovesAnEntryFromAnotherFolderAndKeepsItsSettings()
    {
        Write("{\"modules\": {\"scripts-tool\": [{\"path\": \"C:/repo/obs/TibiaScarabEye.lua\", \"settings\": {\"dx\": 7}}]}}");
        Assert.Equal(0, Run("Register"));
        Assert.Equal(new[] { LuaJson }, Scripts());
        Assert.Contains("\"dx\": 7", File.ReadAllText(Collection()));
    }

    [Fact]
    public void Register_TwiceDoesNotDuplicate()
    {
        Write("{\"name\": \"c\"}");
        Assert.Equal(0, Run("Register"));
        Assert.Equal(0, Run("Register"));
        Assert.Equal(new[] { LuaJson }, Scripts());
    }

    [Fact]
    public void Register_OnlyTouchesTheActiveCollection()
    {
        Write("{\"name\": \"outra\"}", "Outra.json", active: false);
        Write("{\"name\": \"ativa\"}");
        Assert.Equal(0, Run("Register"));
        Assert.Equal(new[] { LuaJson }, Scripts());
        Assert.Empty(Scripts("Outra.json"));
    }

    [Fact]
    public void Unregister_RemovesOnlyThisScript()
    {
        Write("{\"modules\": {\"scripts-tool\": [{\"path\": \"C:/outro.lua\", \"settings\": {}}, {\"path\": \"" + LuaJson + "\", \"settings\": {\"dx\": 1}}], \"x\": {}}}");
        Assert.Equal(0, Run("Unregister"));
        Assert.Equal(new[] { "C:/outro.lua" }, Scripts());

        Write("{\"modules\": {\"scripts-tool\": [{\"path\": \"" + LuaJson + "\", \"settings\": {}}]}}");
        Assert.Equal(0, Run("Unregister"));
        Assert.Empty(Scripts());
    }

    [Fact]
    public void Unregister_LeavesACollectionWithoutTheScriptUntouched()
    {
        string original = "{\"modules\": {\"scripts-tool\": [{\"path\": \"C:/outro.lua\", \"settings\": {}}]}}";
        Write(original);
        Assert.Equal(0, Run("Unregister"));
        Assert.Equal(original, File.ReadAllText(Collection()));
        Assert.False(File.Exists(Collection() + ".scarab-eye.bak"));
    }

    [Fact]
    public void Register_ReportsWhenObsHasNoCollection()
    {
        Assert.Equal(2, Run("Register"));
    }

    [Fact]
    public void Register_RefusesToWriteBrokenJson()
    {
        string broken = "{\"name\": \"c\", ";
        Write(broken);
        Assert.Equal(1, Run("Register"));
        Assert.Equal(broken, File.ReadAllText(Collection()));
    }
}
