using System;
using System.Diagnostics;

namespace TibiaScarabEye.Interop;

// O cliente do Tibia aberto: a janela, o título e o personagem logado ("Tibia - Nome").
internal sealed class TibiaWindow {
    public IntPtr Handle;
    public string Title="";
    public string Character="";
}

// Procura o cliente do Tibia entre as janelas abertas, sem o usuário escolher. Uma janela só conta se o título começa com
// "Tibia" e o processo dela é o cliente (client): abas de navegador com "Tibia - ..." no título não enganam.
internal static class TibiaLocator {
    const string ClientProcess="client";

    // Título "Tibia" (sem personagem) ou "Tibia - Nome". Devolve o nome do personagem, vazio quando não há.
    internal static bool ParseTitle(string title,out string character) {
        character="";
        if(string.IsNullOrEmpty(title)) return false;
        if(title=="Tibia") return true;
        if(!title.StartsWith("Tibia - ",StringComparison.Ordinal)) return false;
        character=title.Substring("Tibia - ".Length).Trim();
        return true;
    }
    internal static bool IsClientProcess(string processName) { return string.Equals(processName,ClientProcess,StringComparison.OrdinalIgnoreCase); }

    // Com vários clientes abertos, mantém o que já estava sendo lido (prefer) em vez de saltar de um para outro.
    public static TibiaWindow Find(IntPtr prefer) {
        TibiaWindow first=null;
        foreach(var window in Native.Windows()) {
            string character;
            if(!ParseTitle(window.Title,out character) || !IsClientProcess(Native.ProcessName(window.Handle))) continue;
            var found=new TibiaWindow { Handle=window.Handle, Title=window.Title, Character=character };
            if(window.Handle==prefer) return found;
            if(first==null) first=found;
        }
        return first;
    }
}
