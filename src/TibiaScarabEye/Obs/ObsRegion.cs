using System.Runtime.Serialization;

namespace TibiaScarabEye.Obs;

[DataContract] internal sealed class ObsRegion {
    [DataMember] public string Id;
    [DataMember] public int Left,Top,Width,Height,Opacity;
    [DataMember] public double CropX,CropY,CropW,CropH,ContentX,ContentY,ContentW,ContentH;
    [DataMember] public bool Visible;
}
