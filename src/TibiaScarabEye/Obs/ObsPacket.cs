using System;
using System.Runtime.Serialization;

namespace TibiaScarabEye.Obs;

[DataContract] internal sealed class ObsPacket {
    [DataMember] public int Version=1,Revision;
    [DataMember] public string Session=Guid.NewGuid().ToString("N");
    [DataMember] public long Heartbeat;
    [DataMember] public ObsState Layout;
}
