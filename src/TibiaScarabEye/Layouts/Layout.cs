using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace TibiaScarabEye.Layouts;

[DataContract] public sealed class Layout {
    [DataMember] public int Version=1;
    [DataMember] public string SourceTitle;
    [DataMember] public bool ObsEnabled;
    [DataMember] public List<RegionSpec> Regions=new List<RegionSpec>();
    public static Layout Load(string path) {
        if (new FileInfo(path).Length>1024*1024) throw new InvalidDataException("Este arquivo é grande demais para ser um layout.");
        Layout result;
        using(var stream=File.OpenRead(path)) result=(Layout)new DataContractJsonSerializer(typeof(Layout)).ReadObject(stream);
        if (result==null || result.Version!=1 || result.Regions==null || result.Regions.Count>30) throw new InvalidDataException("Formato de layout não suportado.");
        foreach(var r in result.Regions) { if(r==null) throw new InvalidDataException("Área ausente."); r.Validate(); }
        return result;
    }
    public void Save(string path) {
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            using(var stream=File.Create(temp)) new DataContractJsonSerializer(typeof(Layout)).WriteObject(stream,this);
            if (File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
        } finally { if(File.Exists(temp)) File.Delete(temp); }
    }
}
