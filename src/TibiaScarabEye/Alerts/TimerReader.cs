using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace TibiaScarabEye.Alerts;

// O que o Tibia mostra embaixo de um item com duração: "30m", "11m" (minutos) e, a partir de 2 minutos para o fim, "1m28"
// (minutos e segundos). Seconds é o tempo restante lido; Exact diz que veio com segundos ("1m28") e não só com minutos ("11m").
internal struct TimerReading {
    public string Text;
    public int Seconds;
    public bool Exact;
    public bool Valid { get { return Text!=null; } }
}

// Lê esse número direto da imagem da área. A fonte do Tibia é uma fonte de pixels fixa (8 linhas de altura, texto cinza-claro
// com contorno escuro): cada caractere é procurado como um desenho exato (com até um pixel de diferença) e os encontrados na
// mesma linha formam o texto. Os desenhos foram tirados de capturas reais do cliente; faltam o 6 e o 9, que ainda não apareceram
// em nenhuma (e o "s" de "58s"). Um texto com um caractere desconhecido não é aceito: nunca se adivinha um tempo.
internal static class TimerReader {
    const int GlyphHeight=8, MaxMismatch=1;
    sealed class Glyph { public char Char; public int Width; public bool[] Bits; }
    static readonly List<Glyph> Glyphs=Build();

    static List<Glyph> Build() {
        var list=new List<Glyph>();
        Add(list,'0',".####.","##..##","##..##","##..##","##..##","##..##","##..##",".####.");
        Add(list,'1',".##.","###.",".##.",".##.",".##.",".##.",".##.","####");
        Add(list,'2',".####.","##..##","....##","...##.","..##..",".##...","##....","######");
        Add(list,'3',".####.","##..##","....##","..###.","....##","....##","##..##",".####.");
        Add(list,'4',"....#.","...##.","..###.",".#.##.","#..##.","######","...##.","...##.");
        Add(list,'5',".#####",".##...",".##...",".####.","....##","....##","##..##",".####.");
        Add(list,'7',"######","....##","...##.","...##.","..##..","..##..",".##...",".##...");
        Add(list,'8',".####.","##..##","##..##",".####.","##..##","##..##","##..##",".####.");
        Add(list,'m',"..........","..........","#####.###.","##..##..##","##..##..##","##..##..##","##..##..##","##..##..##");
        return list;
    }
    static void Add(List<Glyph> list,char ch,params string[] rows) {
        var glyph=new Glyph { Char=ch, Width=rows[0].Length, Bits=new bool[rows[0].Length*GlyphHeight] };
        for(int y=0;y<GlyphHeight;y++) for(int x=0;x<glyph.Width;x++) glyph.Bits[y*glyph.Width+x]=rows[y][x]=='#';
        list.Add(glyph);
    }

    // Pixel de texto: cinza-claro (quase sem cor). Os do item e do fundo têm cor, ou são escuros.
    internal static bool IsText(PixelBuffer area,int x,int y) {
        int i=(y*area.Width+x)*4, b=area.Bgra[i], g=area.Bgra[i+1], r=area.Bgra[i+2];
        int max=Math.Max(r,Math.Max(g,b)), min=Math.Min(r,Math.Min(g,b));
        return (r*299+g*587+b*114)/1000>=150 && max-min<=45;
    }

    struct Hit { public int X, Y, Width, Mismatch; public char Char; }

    // O desenho do texto da área em letras (# = pixel de texto), só as linhas de baixo: vai para o registro quando a leitura falha.
    internal static string Describe(PixelBuffer area) {
        var sb=new StringBuilder();
        for(int y=Math.Max(0,area.Height-14);y<area.Height;y++) {
            for(int x=0;x<area.Width;x++) sb.Append(IsText(area,x,y)?'#':'.');
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }

    // O texto fica logo embaixo do desenho do item (que começa em 'sprite'): só essa faixa é lida, para o texto do slot do lado
    // (em áreas que cobrem vários slots) não se misturar.
    public static TimerReading Read(PixelBuffer area,Point sprite) {
        var band=Rectangle.Intersect(new Rectangle(sprite.X-3,sprite.Y+19,38,14),new Rectangle(0,0,area.Width,area.Height));
        string text=ReadText(area,band);
        return text==null?default(TimerReading):Parse(text);
    }
    // Os desenhos dos caracteres (uma linha por string), para os testes montarem imagens com o texto.
    internal static string[] Rows(char ch) {
        foreach(var glyph in Glyphs) if(glyph.Char==ch) {
            var rows=new string[GlyphHeight];
            for(int y=0;y<GlyphHeight;y++) { var sb=new StringBuilder(); for(int x=0;x<glyph.Width;x++) sb.Append(glyph.Bits[y*glyph.Width+x]?'#':'.'); rows[y]=sb.ToString(); }
            return rows;
        }
        return null;
    }

    // Todos os caracteres achados; vale a linha de texto (a mesma altura) com mais caracteres, da esquerda para a direita.
    internal static string ReadText(PixelBuffer area,Rectangle band) {
        if(band.Width<8 || band.Height<GlyphHeight) return null;
        var mask=new bool[area.Width*area.Height];
        for(int y=band.Top;y<band.Bottom;y++) for(int x=band.Left;x<band.Right;x++) mask[y*area.Width+x]=IsText(area,x,y);
        var hits=new List<Hit>();
        for(int y=band.Top;y<=band.Bottom-GlyphHeight;y++)
            for(int x=band.Left;x<band.Right;x++)
                foreach(var glyph in Glyphs) {
                    if(x+glyph.Width>band.Right) continue;
                    int mismatch=Compare(mask,area.Width,x,y,glyph);
                    if(mismatch<=MaxMismatch) hits.Add(new Hit { X=x, Y=y, Width=glyph.Width, Mismatch=mismatch, Char=glyph.Char });
                }
        if(hits.Count==0) return null;
        // Escolhe a linha com mais caracteres sem sobreposição.
        var byRow=new Dictionary<int,List<Hit>>();
        foreach(var hit in hits) { List<Hit> row; if(!byRow.TryGetValue(hit.Y,out row)) byRow[hit.Y]=row=new List<Hit>(); row.Add(hit); }
        string best=null; int bestCount=0;
        foreach(var pair in byRow) {
            var chosen=NonOverlapping(pair.Value);
            if(chosen.Count>bestCount) { bestCount=chosen.Count; var sb=new StringBuilder(); foreach(var hit in chosen) sb.Append(hit.Char); best=sb.ToString(); }
        }
        return best;
    }
    static List<Hit> NonOverlapping(List<Hit> row) {
        row.Sort(delegate(Hit a,Hit b) { int c=a.X.CompareTo(b.X); return c!=0?c:a.Mismatch.CompareTo(b.Mismatch); });
        var chosen=new List<Hit>();
        foreach(var hit in row) {
            if(chosen.Count>0) {
                var last=chosen[chosen.Count-1];
                if(hit.X<last.X+last.Width) { if(hit.Mismatch<last.Mismatch) chosen[chosen.Count-1]=hit; continue; }
            }
            chosen.Add(hit);
        }
        return chosen;
    }
    static int Compare(bool[] mask,int stride,int left,int top,Glyph glyph) {
        int mismatch=0;
        for(int y=0;y<GlyphHeight;y++) for(int x=0;x<glyph.Width;x++)
            if(mask[(top+y)*stride+left+x]!=glyph.Bits[y*glyph.Width+x] && ++mismatch>MaxMismatch) return mismatch;
        return mismatch;
    }

    // "30m" -> 30 minutos; "1m28" -> 1 minuto e 28 segundos. Qualquer outra forma não vale.
    internal static TimerReading Parse(string text) {
        int m=text.IndexOf('m');
        if(m<=0 || text.IndexOf('m',m+1)>=0) return default(TimerReading);
        int minutes;
        if(!int.TryParse(text.Substring(0,m),out minutes) || minutes<1 || minutes>99) return default(TimerReading);
        string tail=text.Substring(m+1);
        if(tail.Length==0) return new TimerReading { Text=text, Seconds=minutes*60, Exact=false };
        int seconds;
        if(tail.Length!=2 || !int.TryParse(tail,out seconds) || seconds>59 || minutes>9) return default(TimerReading);
        return new TimerReading { Text=text, Seconds=minutes*60+seconds, Exact=true };
    }
}
