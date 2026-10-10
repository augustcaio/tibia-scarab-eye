using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using TibiaScarabEye.Interop;
using TibiaScarabEye.Layouts;
using TibiaScarabEye.Obs;

namespace TibiaScarabEye.UI;

// Janela principal: mostra se o Tibia está sendo lido (ele é detectado sozinho), escolhe o preset do personagem e leva às
// ações. Criar e ajustar áreas (posição, opacidade, tamanho, camadas) acontece no editor. Os presets são salvos sozinhos.
internal sealed class MainForm : FramelessForm {
    sealed class PresetItem {
        public Preset Preset; public string Text;
        public override string ToString() { return Text; }
    }
    const int MinClientWidth=600;
    readonly StatusLamp lamp=new StatusLamp();
    readonly ComboBox presets=new ComboBox();
    readonly Button newPreset, options, editor, mode, visibility, obsCapture;
    readonly Label areaInfo, obsStatus, status;
    readonly ContextMenuStrip menu=new ContextMenuStrip();
    readonly ToolStripMenuItem duplicateItem, renameItem, deleteItem, exportOneItem, exportAllItem, importItem;
    readonly TableLayoutPanel layout;
    readonly List<Overlay> overlays=new List<Overlay>();
    readonly Timer timer=new Timer(), autosave=new Timer();
    readonly ToolTip tips=new ToolTip();
    readonly PresetStore store;
    // Recebe a janela que já estava sendo lida (para preferi-la com vários clientes abertos); os testes trocam por uma falsa.
    readonly Func<IntPtr,TibiaWindow> locator;
    ObsBridge obsOutput;
    Preset active;
    bool materialized, loading, locked, binding, hotkey, visibilityHotkey, overlaysHidden;
    IntPtr source;
    string character="";

    public MainForm() : this(PresetStore.DefaultPath,TibiaLocator.Find) { }
    internal MainForm(string presetsPath,Func<IntPtr,TibiaWindow> locator) {
        this.locator=locator; store=PresetStore.Open(presetsPath);
        Theme.Apply(this); Text="Tibia Scarab Eye"; BandTitle="Scarab Eye"; BandCaption="build "+BuildStamp();
        StartPosition=FormStartPosition.CenterScreen;

        presets.DropDownStyle=ComboBoxStyle.DropDownList; presets.Anchor=AnchorStyles.Left|AnchorStyles.Right; presets.Margin=new Padding(0,0,8,0); Theme.Style(presets);
        newPreset=Theme.Button("Novo",false); newPreset.Width=72; newPreset.Anchor=AnchorStyles.Left|AnchorStyles.Right; newPreset.Margin=new Padding(0,0,6,0);
        options=Theme.Button("Opções",false); options.Width=88; options.Anchor=AnchorStyles.Left|AnchorStyles.Right;
        tips.SetToolTip(newPreset,"Cria um preset novo, vazio, para o personagem atual");
        tips.SetToolTip(options,"Duplicar, renomear, excluir, exportar e importar presets");
        var presetRow=Table(Columns(AutoColumn(),PercentColumn(100),AutoColumn(),AutoColumn()),Theme.Caption("Preset"),presets,newPreset,options);

        areaInfo=Theme.Note("");
        editor=Theme.Button("Editor de áreas",true); obsCapture=Theme.Button("Sincronizar com OBS",false);
        editor.Anchor=obsCapture.Anchor=AnchorStyles.Left|AnchorStyles.Right; editor.Margin=new Padding(0,0,6,0);
        tips.SetToolTip(editor,"Cria e posiciona as áreas sobre a prévia do jogo, antes de elas aparecerem na tela");
        tips.SetToolTip(obsCapture,"A Captura de jogo do Tibia fica direto na cena do OBS; as overlays viram grupos. Usa a imagem já capturada, não recaptura a tela.");
        var actionsA=Table(Columns(PercentColumn(50),PercentColumn(50)),editor,obsCapture);

        mode=Theme.Button("Travar para jogar",true); visibility=Theme.Button("Ocultar overlays",false);
        mode.Anchor=visibility.Anchor=AnchorStyles.Left|AnchorStyles.Right; mode.Margin=new Padding(0,0,6,0);
        tips.SetToolTip(mode,"Ctrl + Shift + F8, mesmo com o painel minimizado"); tips.SetToolTip(visibility,"Ctrl + Shift + F9, mesmo com o painel minimizado");
        var actionsB=Table(Columns(PercentColumn(50),PercentColumn(50)),mode,visibility);

        obsStatus=Theme.Note("No OBS: Ferramentas > Scripts > adicione obs/TibiaScarabEye.lua.");
        status=Theme.Note("");

        var root=Table(Columns(PercentColumn(100)),lamp,presetRow,areaInfo,actionsA,actionsB,obsStatus,status);
        lamp.Margin=new Padding(0,0,0,10); presetRow.Margin=new Padding(0,0,0,6); areaInfo.Margin=new Padding(0,0,0,10);
        actionsA.Margin=new Padding(0,0,0,8); actionsB.Margin=new Padding(0,0,0,10); obsStatus.Margin=new Padding(0,0,0,4);
        root.AutoSize=false; root.Padding=Padding.Empty; layout=root;
        Controls.Add(root);
        ClientSize=new Size(640,350); MinimumSize=SizeFromClientSize(new Size(MinClientWidth,300));

        duplicateItem=Item("Duplicar preset",delegate { DuplicatePreset(); }); renameItem=Item("Renomear…",delegate { RenamePreset(); }); deleteItem=Item("Excluir preset",delegate { DeletePreset(); });
        exportOneItem=Item("Exportar este preset…",delegate { Export(false); }); exportAllItem=Item("Exportar todos os presets…",delegate { Export(true); }); importItem=Item("Importar…",delegate { Import(); });
        menu.Items.AddRange(new ToolStripItem[]{duplicateItem,renameItem,deleteItem,new ToolStripSeparator(),exportOneItem,exportAllItem,importItem});
        menu.Renderer=new ToolStripProfessionalRenderer(new DarkMenuColors()) { RoundedEdges=false }; menu.ForeColor=Theme.Ink; menu.BackColor=Color.FromArgb(39,39,37);

        presets.SelectedIndexChanged+=delegate { var item=presets.SelectedItem as PresetItem; if(!binding && item!=null && item.Preset!=active) SwitchTo(item.Preset); };
        newPreset.Click+=delegate { NewPreset(); };
        options.Click+=delegate { UpdateStates(); menu.Show(options,new Point(0,options.Height)); };
        editor.Click+=delegate { OpenEditor(); };
        obsCapture.Click+=delegate { ToggleObsOutput(); };
        mode.Click+=delegate { ToggleMode(); };
        visibility.Click+=delegate { ToggleVisibility(); };
        Shown+=delegate { hotkey=Native.RegisterHotKey(Handle,1,0x4006,(uint)Keys.F8); visibilityHotkey=Native.RegisterHotKey(Handle,2,0x4006,(uint)Keys.F9); DetectTibia(); UpdateStatus(); };
        timer.Interval=750; timer.Tick+=delegate { DetectTibia(); TickSource(); }; timer.Start();
        autosave.Interval=1500; autosave.Tick+=delegate { SaveActive(); };
        RefreshPresetList(); UpdateStates(); ShowLamp(null);
    }
    ToolStripMenuItem Item(string text,Action click) {
        var item=new ToolStripMenuItem(text) { ForeColor=Theme.Ink };
        item.Click+=delegate { Safe(click); };
        return item;
    }

    // A altura mínima é a que o conteúdo pede na largura mínima (o texto quebra em mais linhas ali). Mede com o layout pronto.
    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        BeginInvoke(new Action(FitMinimumSize));
    }
    void FitMinimumSize() {
        int minWidth=LogicalToDeviceUnits(MinClientWidth), width=ClientSize.Width, height=ClientSize.Height;
        ClientSize=new Size(minWidth,LogicalToDeviceUnits(300)); PerformLayout(); PerformLayout();
        int content=0;
        foreach(int row in layout.GetRowHeights()) content+=row;
        int minHeight=Padding.Vertical+content;
        MinimumSize=SizeFromClientSize(new Size(minWidth,minHeight));
        ClientSize=new Size(width,Math.Max(height,minHeight));
    }
    static ColumnStyle AutoColumn() { return new ColumnStyle(SizeType.AutoSize); }
    static ColumnStyle PercentColumn(float percent) { return new ColumnStyle(SizeType.Percent,percent); }
    static ColumnStyle[] Columns(params ColumnStyle[] columns) { return columns; }
    // Grade que preenche as células em ordem (uma linha a cada 'columns.Length' controles); toda linha começa com altura automática.
    static TableLayoutPanel Table(ColumnStyle[] columns,params Control[] cells) {
        var table=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=columns.Length, BackColor=Color.Transparent, Margin=Padding.Empty, AutoSize=true, AutoSizeMode=AutoSizeMode.GrowAndShrink };
        foreach(var column in columns) table.ColumnStyles.Add(column);
        for(int i=0;i<cells.Length;i++) { if(i%columns.Length==0) table.RowStyles.Add(new RowStyle(SizeType.AutoSize)); table.Controls.Add(cells[i]); }
        return table;
    }
    // Shown in the title so a stale executable is obvious at a glance.
    static string BuildStamp() {
        try { return File.GetLastWriteTime(Application.ExecutablePath).ToString("dd/MM HH:mm"); } catch { return "?"; }
    }
    void Safe(Action action) { try { action(); } catch(Exception ex) { MessageBox.Show(this,ex.Message,"Tibia Scarab Eye",MessageBoxButtons.OK,MessageBoxIcon.Warning); } }

    // ----- O Tibia: detectado sozinho -----
    void DetectTibia() {
        var found=locator(source);
        if(found==null) { if(source!=IntPtr.Zero) DetachTibia(); ShowLamp(null); return; }
        if(found.Handle!=source) AttachTibia(found);
        else if(!string.Equals(found.Character??"",character,StringComparison.OrdinalIgnoreCase)) ChangeCharacter(found.Character);
        ShowLamp(found);
    }
    void AttachTibia(TibiaWindow found) {
        SaveActive();
        source=found.Handle; character=found.Character??"";
        Safe(delegate { LoadPreset(active!=null && active.AppliesTo(character)?active:store.Pick(character)); });
    }
    // O mesmo cliente, outro personagem (saiu e entrou): troca para o preset dele.
    void ChangeCharacter(string newCharacter) {
        SaveActive();
        character=newCharacter??"";
        Safe(delegate { LoadPreset(active!=null && active.AppliesTo(character)?active:store.Pick(character)); });
    }
    void DetachTibia() {
        SaveActive();
        StopObsOutput(); ClearOverlays();
        source=IntPtr.Zero; locked=false; mode.Text="Travar para jogar";
        RefreshPresetList(); UpdateStates();
    }
    void ShowLamp(TibiaWindow found) {
        if(found==null) lamp.Set(LampState.Searching,"Procurando o Tibia","Abra o jogo: ele é detectado sozinho.");
        else if(Native.IsIconic(found.Handle)) lamp.Set(LampState.Minimized,"Tibia minimizado","Restaure a janela do jogo para continuar.");
        else lamp.Set(LampState.Reading,"Lendo o Tibia",character.Length>0?character:"nenhum personagem conectado");
    }
    bool Ready() {
        if(source==IntPtr.Zero || !Native.IsWindow(source)) { MessageBox.Show(this,"O Tibia não foi encontrado. Abra o jogo: o programa o detecta sozinho."); return false; }
        if(Native.IsIconic(source)) { MessageBox.Show(this,"O Tibia está minimizado. Restaure a janela para continuar."); return false; }
        return true;
    }

    // ----- Presets: salvos sozinhos -----
    void MarkChanged() {
        if(loading) return;
        autosave.Stop(); autosave.Start(); UpdateStates();
    }
    List<RegionSpec> Snapshot() {
        var list=new List<RegionSpec>();
        foreach(var overlay in overlays) { overlay.CaptureSpec(); list.Add(overlay.Spec.Clone()); }
        return list;
    }
    // Guarda as áreas atuais no preset ativo. Só vale com as overlays montadas; sem o Tibia aberto o preset fica como está.
    void SaveActive() {
        autosave.Stop();
        if(active==null || !materialized) return;
        active.Regions=Snapshot();
        SaveStore();
    }
    void SaveStore() {
        try { store.Save(); } catch(Exception ex) { status.Text="Não foi possível salvar os presets: "+ex.Message; }
    }
    // Troca o preset ativo: monta as overlays dele (se o Tibia estiver aberto) e lembra dele para este personagem.
    void LoadPreset(Preset preset) {
        loading=true;
        try {
            ClearOverlays(false);
            active=preset;
            if(preset!=null && source!=IntPtr.Zero) {
                foreach(var spec in preset.Regions) CreateOverlay(spec.Clone());
                materialized=true;
                store.SetLastUsed(character,preset.Id); SaveStore();
            }
        } finally { loading=false; }
        RefreshPresetList(); UpdateStates(); TickSource();
    }
    void SwitchTo(Preset preset) { SaveActive(); Safe(delegate { LoadPreset(preset); }); }
    string ItemText(Preset preset) {
        if(preset.IsGlobal) return preset.Name+" (todos)";
        return source!=IntPtr.Zero?preset.Name:preset.Name+" ("+preset.Character+")";
    }
    void RefreshPresetList() {
        binding=true;
        presets.Items.Clear();
        foreach(var preset in store.Presets) {
            if(source!=IntPtr.Zero && !preset.AppliesTo(character)) continue;
            presets.Items.Add(new PresetItem { Preset=preset, Text=ItemText(preset) });
            if(preset==active) presets.SelectedIndex=presets.Items.Count-1;
        }
        binding=false;
    }
    static string Plural(int count) { return count==1?"1 área":count+" áreas"; }
    void UpdateStates() {
        bool tibia=source!=IntPtr.Zero;
        editor.Enabled=tibia; obsCapture.Enabled=mode.Enabled=visibility.Enabled=overlays.Count>0;
        presets.Enabled=presets.Items.Count>0;
        duplicateItem.Enabled=renameItem.Enabled=deleteItem.Enabled=exportOneItem.Enabled=active!=null;
        exportAllItem.Enabled=store.Presets.Count>0;
        if(locked) areaInfo.Text="Modo jogo: as áreas aparecem sobre o jogo e deixam o mouse passar.";
        else if(!tibia) areaInfo.Text=active!=null?"Abra o Tibia para editar. "+Plural(active.Regions.Count)+" neste preset.":"Abra o Tibia para criar áreas. Seus presets ficam salvos neste programa.";
        else if(active==null) areaInfo.Text="Nenhum preset ainda. Abra o editor para criar o primeiro ou use Novo.";
        else areaInfo.Text=Plural(overlays.Count)+" neste preset. As áreas só aparecem na tela quando você trava para jogar.";
    }
    Preset EnsureActive() {
        if(active!=null) return active;
        active=store.Add("Padrão",character.Length>0?character:null,new RegionSpec[0]);
        materialized=source!=IntPtr.Zero;
        store.SetLastUsed(character,active.Id); SaveStore();
        RefreshPresetList(); UpdateStates();
        return active;
    }
    // Pergunta o nome e, com o Tibia aberto e um personagem logado, se vale para todos.
    bool AskName(string title,string initial,bool initialEveryone,out string name,out bool everyone) {
        name=initial; everyone=initialEveryone;
        bool offer=source!=IntPtr.Zero && character.Length>0;
        return PromptForm.Ask(this,title,"Nome do preset",ref name,offer,ref everyone);
    }
    void NewPreset() {
        string name=store.UniqueName("Preset "+(store.Presets.Count+1),character.Length>0?character:null,null); bool everyone=false;
        if(!AskName("Novo preset",name,false,out name,out everyone)) return;
        SaveActive();
        var preset=store.Add(name,everyone||character.Length==0?null:character,new RegionSpec[0]);
        SaveStore();
        Safe(delegate { LoadPreset(preset); });
    }
    void RenamePreset() {
        if(active==null) return;
        string name=active.Name; bool everyone;
        if(!AskName("Renomear preset",name,active.IsGlobal,out name,out everyone)) return;
        string owner=everyone?null:(character.Length>0?character:active.Character);
        store.Rename(active,name,owner); SaveStore(); RefreshPresetList(); UpdateStates();
    }
    void DuplicatePreset() {
        if(active==null) return;
        SaveActive();
        var copies=new List<RegionSpec>();
        foreach(var region in active.Regions) copies.Add(PresetStore.Fresh(region));
        var preset=store.Add(active.Name+" cópia",active.Character,copies);
        SaveStore();
        Safe(delegate { LoadPreset(preset); });
    }
    void DeletePreset() {
        if(active==null) return;
        if(MessageBox.Show(this,"Excluir o preset \""+active.Name+"\"? Isso não pode ser desfeito.","Excluir preset",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes) return;
        store.Remove(active); SaveStore();
        active=null;
        Safe(delegate { LoadPreset(store.Pick(character)); });
    }
    void Export(bool all) {
        if(!all && active==null) return;
        SaveActive();
        var list=new List<Preset>();
        if(all) list.AddRange(store.Presets); else list.Add(active);
        string suggestion=all?"Presets Tibia Scarab Eye":active.Name;
        foreach(char invalid in Path.GetInvalidFileNameChars()) suggestion=suggestion.Replace(invalid,'_');
        using(var dialog=new SaveFileDialog { Filter="Presets do Tibia Scarab Eye (*.json)|*.json", FileName=suggestion+".json", Title=all?"Exportar todos os presets":"Exportar preset" }) {
            if(dialog.ShowDialog(this)!=DialogResult.OK) return;
            store.Export(dialog.FileName,list);
            status.Text=(list.Count==1?"Preset exportado":list.Count+" presets exportados")+" para "+dialog.FileName;
        }
    }
    void Import() {
        using(var dialog=new OpenFileDialog { Filter="Presets ou layouts do Tibia Scarab Eye (*.json)|*.json", Title="Importar presets" }) {
            if(dialog.ShowDialog(this)!=DialogResult.OK) return;
            int count=store.Import(dialog.FileName);
            RefreshPresetList(); UpdateStates();
            status.Text=count==1?"1 preset importado.":count+" presets importados.";
            if(active==null && source!=IntPtr.Zero) Safe(delegate { LoadPreset(store.Pick(character)); });
        }
    }

    // ----- Editor e overlays -----
    // Criar e posicionar acontecem no mesmo lugar: o editor mostra o jogo ao vivo e age sobre as overlays na hora.
    void OpenEditor() {
        if(!Ready()) return;
        if(locked) ToggleMode();
        Safe(delegate {
            EnsureActive();
            using(var area=new AreaEditor(source,overlays,CreateOverlay,RemoveOverlay,-1)) {
                area.Changed+=delegate { MarkChanged(); };
                area.ShowDialog(this);
            }
            SaveActive(); UpdateStates();
        });
    }
    Overlay CreateOverlay(RegionSpec spec) {
        var overlay=new Overlay(source,spec);
        overlay.SetObsMode(false);
        try { overlay.Prepare(); }
        catch { overlay.Dispose(); throw; }
        overlay.Changed+=delegate { MarkChanged(); };
        if(locked) overlay.SetLocked(true);
        overlays.Add(overlay); MarkChanged();
        return overlay;
    }
    void RemoveOverlay(Overlay overlay) {
        int index=overlays.IndexOf(overlay); if(index<0) return;
        overlays[index].Close(); overlays[index].Dispose(); overlays.RemoveAt(index);
        if(overlays.Count==0) { StopObsOutput(); locked=false; mode.Text="Travar para jogar"; }
        MarkChanged();
    }
    void ClearOverlays() { ClearOverlays(true); }
    void ClearOverlays(bool stopObs) {
        if(stopObs) StopObsOutput();
        foreach(var overlay in overlays) { overlay.Close(); overlay.Dispose(); }
        overlays.Clear(); materialized=false;
        if(overlays.Count==0) UpdateStates();
    }

    void ToggleObsOutput() {
        if(obsOutput!=null) { StopObsOutput(); return; }
        if(overlays.Count==0 || !Ready()) return;
        Safe(delegate {
            var output=new ObsBridge(source,overlays); obsOutput=output;
            output.Failed+=delegate(string error) { obsStatus.Text=error; };
            try { output.Start(); } catch { obsOutput=null; output.Dispose(); throw; }
            obsCapture.Text="Parar sincronização";
            obsStatus.Text="Layout enviado. No OBS, os grupos Tibia Scarab Eye aparecem no topo da cena.";
        });
    }
    void StopObsOutput() {
        if(obsOutput!=null) { var output=obsOutput; obsOutput=null; output.Dispose(); }
        obsCapture.Text="Sincronizar com OBS";
        obsStatus.Text="Sincronização desligada. O grupo fica transparente no OBS.";
    }
    void ToggleVisibility() {
        overlaysHidden=!overlaysHidden;
        visibility.Text=overlaysHidden?"Mostrar overlays":"Ocultar overlays";
        TickSource(); UpdateStatus();
    }
    void ToggleMode() {
        if(overlays.Count==0) return;
        Safe(delegate {
            locked=!locked;
            foreach(var overlay in overlays) overlay.SetLocked(locked);
            TickSource();
            mode.Text=locked?"Destravar para editar":"Travar para jogar";
            UpdateStates();
        });
    }
    void UpdateStatus() {
        status.Text=hotkey?"Ctrl + Shift + F8 trava e edita"+(visibilityHotkey?"; F9 mostra e oculta":"")+".":"Atalho indisponível. Use o botão Travar para jogar.";
        if(overlaysHidden) status.Text="Overlays ocultas. Ctrl + Shift + F9 ou Mostrar overlays para restaurar.";
        if(!visibilityHotkey) status.Text+="\nF9 indisponível: use o botão Mostrar/Ocultar overlays.";
    }
    void TickSource() {
        if(overlays.Count==0) return;
        // As overlays só aparecem no modo jogo, e as escondidas (camada) nunca; no modo edição o editor é o único lugar onde elas existem.
        bool alive=Native.IsWindow(source), ready=alive && !Native.IsIconic(source) && !overlaysHidden, visible=ready && locked;
        bool shown=false;
        foreach(var overlay in overlays) {
            bool want=visible && !overlay.Spec.Hidden;
            if(want && !overlay.Visible) { overlay.Show(); shown=true; }
            if(!want && overlay.Visible) overlay.Hide();
            if(want) { try { overlay.Render(); } catch { visible=false; overlay.Hide(); } }
        }
        // A ordem das camadas é a ordem da lista (a última fica por cima); reaplica quando alguma acabou de aparecer.
        if(shown) foreach(var overlay in overlays) if(overlay.Visible) overlay.BringToTop();
        if(locked && !visible && !overlaysHidden) status.Text="Recortes pausados. Restaure a janela do Tibia para continuar.";
        else UpdateStatus();
    }
    protected override void WndProc(ref Message m) { if(m.Msg==0x312) { if(m.WParam.ToInt32()==1) { ToggleMode(); return; } if(m.WParam.ToInt32()==2) { ToggleVisibility(); return; } } base.WndProc(ref m); }
    protected override void OnFormClosing(FormClosingEventArgs e) {
        timer.Stop(); SaveActive(); ClearOverlays();
        Native.UnregisterHotKey(Handle,1); Native.UnregisterHotKey(Handle,2);
        base.OnFormClosing(e);
    }
    protected override void Dispose(bool disposing) { if(disposing) { timer.Dispose(); autosave.Dispose(); tips.Dispose(); menu.Dispose(); } base.Dispose(disposing); }
}
