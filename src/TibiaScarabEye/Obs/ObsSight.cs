using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TibiaScarabEye.Alerts;

namespace TibiaScarabEye.Obs;

// As imagens das áreas recortadas, enviadas pelo script do OBS. O cliente do Tibia não deixa o Windows copiar a janela dele (a
// captura sai preta), mas o OBS já enxerga o jogo pela Captura de jogo; o script recorta as áreas dessa mesma imagem e grava os
// pixels em obs-sight.bin. O programa só lê esse arquivo: nada é lido do jogo nem injetado nele.
//
// Formato (binário):
//   TSEYE1\n
//   <heartbeat em segundos Unix> <sessão> <quantidade de áreas> <sequência>\n   (a sequência cresce a cada quadro gravado)
//   por área: <largura> <altura>\n seguido de largura*altura*4 bytes (BGRA, linha a linha, sem folga)
internal sealed class ObsSight {
    public const string Magic="TSEYE1";
    public const int MaxAreas=30, MaxSide=512;
    public static readonly string DefaultPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TibiaScarabEye","obs-sight.bin");
    public long Heartbeat;
    public string Session="";
    public long Sequence;
    public readonly List<PixelBuffer> Areas=new List<PixelBuffer>();

    public static ObsSight Parse(byte[] data) {
        int at=0;
        if(ReadLine(data,ref at)!=Magic) throw new InvalidDataException("Arquivo de imagens do OBS desconhecido.");
        string[] head=ReadLine(data,ref at).Split(' ');
        long heartbeat; int count;
        // Versão anterior do script (ainda carregada no OBS depois de atualizar o programa): sem sequência, que vale 0.
        long sequence=0;
        if((head.Length!=3 && head.Length!=4) || !long.TryParse(head[0],out heartbeat) || !int.TryParse(head[2],out count) || (head.Length==4 && !long.TryParse(head[3],out sequence)) || count<0 || count>MaxAreas) throw new InvalidDataException("Cabeçalho inválido.");
        var sight=new ObsSight { Heartbeat=heartbeat, Session=head[1], Sequence=sequence };
        for(int i=0;i<count;i++) {
            string[] size=ReadLine(data,ref at).Split(' ');
            int width, height;
            if(size.Length!=2 || !int.TryParse(size[0],out width) || !int.TryParse(size[1],out height) || width<1 || height<1 || width>MaxSide || height>MaxSide) throw new InvalidDataException("Tamanho de área inválido.");
            int bytes=width*height*4;
            if(data.Length-at<bytes) throw new InvalidDataException("Arquivo cortado.");
            var pixels=new byte[bytes];
            Buffer.BlockCopy(data,at,pixels,0,bytes); at+=bytes;
            sight.Areas.Add(new PixelBuffer(width,height,pixels));
        }
        return sight;
    }
    static string ReadLine(byte[] data,ref int at) {
        int end=Array.IndexOf(data,(byte)'\n',at);
        if(end<0 || end-at>200) throw new InvalidDataException("Linha inválida.");
        string line=Encoding.ASCII.GetString(data,at,end-at);
        at=end+1;
        return line;
    }
}

// Lê o arquivo do OBS a cada pedido (o script grava cerca de 10 quadros por segundo). O script troca o arquivo por inteiro; se ele estiver sumido por um instante, vale a última
// leitura boa por alguns segundos. Devolve null quando não há imagens recentes (OBS fechado, script parado ou sem Captura de jogo).
internal sealed class ObsSightReader {
    public const int MaxAgeSeconds=4;
    const long MaxFileBytes=16*1024*1024;
    readonly string path;
    readonly Func<long> unixNow;
    ObsSight last;
    public ObsSightReader() : this(ObsSight.DefaultPath,null) { }
    public ObsSightReader(string path,Func<long> unixNow) {
        this.path=path;
        this.unixNow=unixNow??delegate { return (long)(DateTime.UtcNow-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds; };
    }
    public IReadOnlyList<PixelBuffer> Read() {
        try {
            if(File.Exists(path)) {
                byte[] bytes;
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
                    if(stream.Length>MaxFileBytes) throw new InvalidDataException("Arquivo grande demais.");
                    bytes=new byte[stream.Length];
                    int read=0;
                    while(read<bytes.Length) { int n=stream.Read(bytes,read,bytes.Length-read); if(n<=0) break; read+=n; }
                    if(read<bytes.Length) throw new IOException("Leitura incompleta.");
                }
                var parsed=ObsSight.Parse(bytes);
                // O mesmo quadro lido de novo mantém a mesma lista: quem lê sabe que não é uma observação nova.
                // Sem sequência (script antigo, 0) cada leitura vale como quadro novo.
                if(last==null || parsed.Sequence==0 || last.Session!=parsed.Session || last.Sequence!=parsed.Sequence) last=parsed; else last.Heartbeat=parsed.Heartbeat;
            }
        } catch(Exception ex) when(ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException) { }
        if(last==null) return null;
        long age=unixNow()-last.Heartbeat;
        return age>MaxAgeSeconds || age<-MaxAgeSeconds?null:last.Areas;
    }
}
