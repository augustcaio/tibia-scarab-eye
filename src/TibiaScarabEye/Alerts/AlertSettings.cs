using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace TibiaScarabEye.Alerts;

// Os interruptores dos alertas (um por item). Valem para o programa todo, não por preset; ligados por padrão.
[DataContract] internal sealed class AlertSettings {
    public static readonly string DefaultPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TibiaScarabEye","alerts.json");
    [DataMember] public int Version=1;
    [DataMember] public bool CollarEnabled=true;
    [DataMember] public bool RingEnabled=true;

    public bool IsEnabled(PlasmaItem item) { return item==PlasmaItem.Collar?CollarEnabled:RingEnabled; }
    public void Set(PlasmaItem item,bool enabled) { if(item==PlasmaItem.Collar) CollarEnabled=enabled; else RingEnabled=enabled; }

    // Arquivo ausente ou ilegível: começa com tudo ligado, sem incomodar quem abre o programa.
    public static AlertSettings Load(string path) {
        try {
            if(!File.Exists(path) || new FileInfo(path).Length>64*1024) return new AlertSettings();
            using(var stream=File.OpenRead(path)) {
                var loaded=new DataContractJsonSerializer(typeof(AlertSettings)).ReadObject(stream) as AlertSettings;
                return loaded!=null && loaded.Version==1?loaded:new AlertSettings();
            }
        } catch(Exception ex) when(ex is IOException || ex is SerializationException || ex is UnauthorizedAccessException) { return new AlertSettings(); }
    }

    // Escrita atômica, como nos presets.
    public void Save(string path) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            using(var stream=File.Create(temp)) new DataContractJsonSerializer(typeof(AlertSettings)).WriteObject(stream,this);
            if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
        } finally { if(File.Exists(temp)) File.Delete(temp); }
    }
}
