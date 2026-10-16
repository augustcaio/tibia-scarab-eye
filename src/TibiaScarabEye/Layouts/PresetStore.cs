using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;

namespace TibiaScarabEye.Layouts;

// Presets guardados no disco (um arquivo JSON em %LOCALAPPDATA%\TibiaScarabEye). Cada alteração é salva por quem usa a classe
// chamando Save. Exportar e importar usam o mesmo formato, e também abrem o arquivo de layout das versões antigas.
public sealed class PresetStore {
    public const int MaxPresets=200;
    const long MaxFileBytes=5*1024*1024;
    public static readonly string DefaultPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TibiaScarabEye","presets.json");
    readonly string path;
    readonly PresetFile data;
    PresetStore(string path,PresetFile data) { this.path=path; this.data=data; }

    public IReadOnlyList<Preset> Presets { get { return data.Presets; } }

    // Abre o arquivo; se não existir começa vazio, e se estiver corrompido guarda uma cópia ao lado e começa vazio.
    public static PresetStore Open(string path) {
        PresetFile data=null;
        if(File.Exists(path)) {
            try { data=Normalize(ReadFile(path)); }
            catch(Exception ex) when(ex is InvalidDataException || ex is System.Runtime.Serialization.SerializationException || ex is IOException) {
                try { File.Copy(path,path+".corrompido",true); } catch(IOException) { }
            }
        }
        return new PresetStore(path,data??new PresetFile());
    }

    public void Save() {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        Write(path,data);
    }

    public Preset Find(string id) {
        foreach(var preset in data.Presets) if(preset.Id==id) return preset;
        return null;
    }

    // Cópia de uma área com identidade nova (id e título do OBS), para duplicar ou importar sem dividir as posições do OBS.
    public static RegionSpec Fresh(RegionSpec region) {
        var copy=region.Clone(); copy.ObsId=null; copy.ObsTitle=null; return copy;
    }

    public Preset Add(string name,string character,IEnumerable<RegionSpec> regions) {
        if(data.Presets.Count>=MaxPresets) throw new InvalidOperationException("O limite é de "+MaxPresets+" presets.");
        character=string.IsNullOrWhiteSpace(character)?null:character.Trim();
        var preset=new Preset { Id=Guid.NewGuid().ToString("N"), Name=UniqueName(name,character,null), Character=character };
        foreach(var region in regions) preset.Regions.Add(region.Clone());
        data.Presets.Add(preset);
        return preset;
    }

    public void Rename(Preset preset,string name,string character) {
        character=string.IsNullOrWhiteSpace(character)?null:character.Trim();
        preset.Name=UniqueName(name,character,preset); preset.Character=character;
    }

    public void Remove(Preset preset) {
        data.Presets.Remove(preset);
        data.LastUsed.RemoveAll(delegate(LastUsed entry) { return entry.PresetId==preset.Id; });
    }

    // Nome único entre os presets do mesmo escopo (mesmo personagem, ou todos): "Combate", "Combate (2)", ...
    public string UniqueName(string wanted,string character,Preset except) {
        string name=Clean(wanted), candidate=name;
        for(int n=2;Taken(candidate,character,except);n++) candidate=name+" ("+n+")";
        return candidate;
    }
    bool Taken(string name,string character,Preset except) {
        foreach(var preset in data.Presets)
            if(preset!=except && string.Equals(preset.Character??"",character??"",StringComparison.OrdinalIgnoreCase) && string.Equals(preset.Name,name,StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    static string Clean(string name) {
        name=(name??"").Trim();
        if(name.Length==0) name="Preset";
        return name.Length>80?name.Substring(0,80).TrimEnd():name;
    }

    public void SetLastUsed(string character,string presetId) {
        character=character??"";
        foreach(var entry in data.LastUsed) if(string.Equals(entry.Character,character,StringComparison.OrdinalIgnoreCase)) { entry.PresetId=presetId; return; }
        data.LastUsed.Add(new LastUsed { Character=character, PresetId=presetId });
    }
    string LastUsedFor(string character) {
        foreach(var entry in data.LastUsed) if(string.Equals(entry.Character,character??"",StringComparison.OrdinalIgnoreCase)) return entry.PresetId;
        return null;
    }

    // O preset a abrir quando um personagem entra: o último que ele usou; senão o primeiro só dele; senão o primeiro de todos.
    public Preset Pick(string character) {
        character=character??"";
        var last=Find(LastUsedFor(character));
        if(last!=null && last.AppliesTo(character)) return last;
        foreach(var preset in data.Presets) if(!preset.IsGlobal && preset.AppliesTo(character)) return preset;
        foreach(var preset in data.Presets) if(preset.IsGlobal) return preset;
        return null;
    }

    public void Export(string file,IEnumerable<Preset> presets) {
        var export=new PresetFile();
        foreach(var preset in presets) {
            var copy=new Preset { Id=preset.Id, Name=preset.Name, Character=preset.Character };
            foreach(var region in preset.Regions) copy.Regions.Add(region.Clone());
            export.Presets.Add(copy);
        }
        Write(file,export);
    }

    // Importa presets de um arquivo exportado ou de um layout salvo por versões antigas. Os importados ganham ids novos e
    // nomes que não colidem com os que já existem. Devolve quantos entraram.
    public int Import(string file) {
        var incoming=ReadAny(file);
        if(data.Presets.Count+incoming.Count>MaxPresets) throw new InvalidDataException("Importar "+incoming.Count+" presets passaria do limite de "+MaxPresets+".");
        foreach(var preset in incoming) {
            var fresh=new List<RegionSpec>();
            foreach(var region in preset.Regions) fresh.Add(Fresh(region));
            Add(preset.Name,preset.Character,fresh);
        }
        Save();
        return incoming.Count;
    }

    static List<Preset> ReadAny(string file) {
        var info=new FileInfo(file);
        if(info.Length>MaxFileBytes) throw new InvalidDataException("Este arquivo é grande demais para ser de presets.");
        PresetFile parsed;
        try { parsed=ReadFile(file); }
        catch(System.Runtime.Serialization.SerializationException) { throw new InvalidDataException("Este arquivo não é de presets nem de layout do Tibia Scarab Eye."); }
        if(parsed.Presets!=null) return Normalize(parsed).Presets;
        // Sem "Presets": pode ser um layout salvo pelas versões antigas, que vira um preset com o nome do arquivo.
        var layout=Layout.Load(file);
        var legacy=new Preset { Name=Path.GetFileNameWithoutExtension(file) };
        legacy.Regions.AddRange(layout.Regions);
        return new List<Preset> { legacy };
    }

    static PresetFile ReadFile(string file) {
        if(new FileInfo(file).Length>MaxFileBytes) throw new InvalidDataException("Este arquivo é grande demais para ser de presets.");
        using(var stream=File.OpenRead(file)) return (PresetFile)new DataContractJsonSerializer(typeof(PresetFile)).ReadObject(stream);
    }

    // Valida o que veio do disco ou de fora: versão, limites e cada área.
    static PresetFile Normalize(PresetFile file) {
        if(file==null || file.Version!=1 || file.Presets==null) throw new InvalidDataException("Formato de presets não suportado.");
        if(file.Presets.Count>MaxPresets) throw new InvalidDataException("Presets demais no arquivo.");
        if(file.LastUsed==null) file.LastUsed=new List<LastUsed>();
        foreach(var preset in file.Presets) {
            if(preset==null) throw new InvalidDataException("Preset ausente.");
            if(string.IsNullOrEmpty(preset.Id)) preset.Id=Guid.NewGuid().ToString("N");
            preset.Name=Clean(preset.Name);
            preset.Character=string.IsNullOrWhiteSpace(preset.Character)?null:preset.Character.Trim();
            if(preset.Regions==null) preset.Regions=new List<RegionSpec>();
            if(preset.Regions.Count>30) throw new InvalidDataException("O preset \""+preset.Name+"\" tem áreas demais.");
            foreach(var region in preset.Regions) { if(region==null) throw new InvalidDataException("Área ausente."); region.Validate(); }
        }
        file.LastUsed.RemoveAll(delegate(LastUsed entry) { return entry==null || entry.PresetId==null; });
        return file;
    }

    // Escrita atômica: grava ao lado e troca, para um arquivo bom nunca ficar pela metade.
    static void Write(string file,PresetFile data) {
        string temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            using(var stream=File.Create(temp)) new DataContractJsonSerializer(typeof(PresetFile)).WriteObject(stream,data);
            if(File.Exists(file)) File.Replace(temp,file,null); else File.Move(temp,file);
        } finally { if(File.Exists(temp)) File.Delete(temp); }
    }
}
