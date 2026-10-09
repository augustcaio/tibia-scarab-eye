using System.Collections.Generic;
using TibiaScarabEye.Layouts;

namespace TibiaScarabEye.UI;

// Desfazer/refazer do editor. Cada passo guarda o estado completo antes e depois (uma cópia de cada RegionSpec, na ordem das
// camadas), então criar, mover, editar recorte, reordenar, travar e apagar usam o mesmo mecanismo. As áreas são
// identificadas por ObsId, que sobrevive a apagar e recriar a overlay.
internal sealed class EditorHistory {
    internal sealed class Snapshot {
        public readonly List<RegionSpec> Areas=new List<RegionSpec>();
        public bool SameAs(Snapshot other) {
            if(other==null || other.Areas.Count!=Areas.Count) return false;
            for(int i=0;i<Areas.Count;i++) if(!Areas[i].SameAs(other.Areas[i])) return false;
            return true;
        }
    }
    sealed class Entry { public Snapshot Before, After; }
    const int Limit=100;
    readonly List<Entry> undo=new List<Entry>(), redo=new List<Entry>();
    public bool CanUndo { get { return undo.Count>0; } }
    public bool CanRedo { get { return redo.Count>0; } }
    public void Push(Snapshot before,Snapshot after) {
        if(before.SameAs(after)) return;
        undo.Add(new Entry { Before=before, After=after });
        if(undo.Count>Limit) undo.RemoveAt(0);
        redo.Clear();
    }
    // Devolvem o estado a restaurar.
    public Snapshot Undo() { var entry=undo[undo.Count-1]; undo.RemoveAt(undo.Count-1); redo.Add(entry); return entry.Before; }
    public Snapshot Redo() { var entry=redo[redo.Count-1]; redo.RemoveAt(redo.Count-1); undo.Add(entry); return entry.After; }
}
