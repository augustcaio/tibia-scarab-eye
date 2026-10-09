using System;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;

namespace TibiaScarabEye.Layouts;

[DataContract] public sealed class RegionSpec {
    [DataMember] public string Name;
    [DataMember] public string ObsId;
    [DataMember] public string ObsTitle;
    [DataMember] public double X, Y, W, H;
    [DataMember] public int Left, Top, Width, Height;
    [DataMember] public int Opacity=100;
    public override string ToString() { return Name; }
    public Rectangle Crop(Size size) {
        int left=Math.Max(0,Math.Min(size.Width-1,(int)Math.Round(X*size.Width)));
        int top=Math.Max(0,Math.Min(size.Height-1,(int)Math.Round(Y*size.Height)));
        int right=Math.Max(left+1,Math.Min(size.Width,(int)Math.Round((X+W)*size.Width)));
        int bottom=Math.Max(top+1,Math.Min(size.Height,(int)Math.Round((Y+H)*size.Height)));
        return Rectangle.FromLTRB(left,top,right,bottom);
    }
    public void Validate() {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length>80 || !Finite(X) || !Finite(Y) || !Finite(W) || !Finite(H) || X<0 || Y<0 || W<=0 || H<=0 || X+W>1.00001 || Y+H>1.00001 || Width<32 || Height<20 || Width>10000 || Height>10000 || Opacity<20 || Opacity>100)
            throw new InvalidDataException("O arquivo contém uma área inválida.");
    }
    static bool Finite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
}
