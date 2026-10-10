using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace TibiaScarabEye.Layouts;

// Um conjunto de áreas com nome. Character vazio quer dizer "todos os personagens"; com nome, o preset só aparece quando
// esse personagem está logado no Tibia. A ordem das Regions é a ordem das camadas.
[DataContract] public sealed class Preset {
    [DataMember] public string Id;
    [DataMember] public string Name;
    [DataMember] public string Character;
    [DataMember] public List<RegionSpec> Regions=new List<RegionSpec>();
    public bool IsGlobal { get { return string.IsNullOrEmpty(Character); } }
    public bool AppliesTo(string character) { return IsGlobal || string.Equals(Character,character,StringComparison.OrdinalIgnoreCase); }
}

// Último preset usado por personagem: ao entrar com esse personagem, é ele que volta.
[DataContract] public sealed class LastUsed {
    [DataMember] public string Character;
    [DataMember] public string PresetId;
}

// Formato do arquivo de presets, no disco e na exportação.
[DataContract] public sealed class PresetFile {
    [DataMember] public int Version=1;
    [DataMember] public List<Preset> Presets=new List<Preset>();
    [DataMember] public List<LastUsed> LastUsed=new List<LastUsed>();
}
