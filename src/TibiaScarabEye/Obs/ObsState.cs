using System.Collections.Generic;
using System.Runtime.Serialization;

namespace TibiaScarabEye.Obs;

[DataContract] internal sealed class ObsState {
    [DataMember] public bool Enabled;
    [DataMember] public int Width,Height;
    [DataMember] public List<ObsRegion> Regions=new List<ObsRegion>();
}
