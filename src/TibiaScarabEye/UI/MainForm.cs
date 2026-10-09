using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using TibiaScarabEye.Interop;
using TibiaScarabEye.Layouts;
using TibiaScarabEye.Obs;

namespace TibiaScarabEye.UI;

internal sealed class MainForm : FramelessForm {
    readonly ComboBox windows=new ComboBox(), zoom=new ComboBox();
    readonly ListBox areas=new ListBox();
    readonly TrackBar opacity=new TrackBar();
    readonly NumericUpDown gridCell=new NumericUpDown();
    readonly Label status, detail, opacityLabel;
    readonly Button mode;
    readonly Button edit, visibility;
    readonly Button obsCapture;
    readonly Label obsStatus;
    ObsBridge obsOutput;
    readonly List<Overlay> overlays=new List<Overlay>();
    readonly Timer timer=new Timer();
    GridOverlay grid;
    RegionSpec mapRegion;
    IntPtr source;
    string sourceTitle="";
    bool locked, binding, dirty, hotkey, visibilityHotkey, overlaysHidden;
    readonly ToolTip tips=new ToolTip();
    const int BodyRow=1, MinClientWidth=580;
    readonly TableLayoutPanel layout, body, side;
    public MainForm() {
        Theme.Apply(this); Text="Tibia Scarab Eye"; BandTitle="Scarab Eye"; BandCaption="build "+BuildStamp();
         StartPosition=FormStartPosition.CenterScreen;

        windows.DropDownStyle=ComboBoxStyle.DropDownList; windows.Anchor=AnchorStyles.Left|AnchorStyles.Right; windows.Margin=new Padding(0,0,8,0);
        zoom.DropDownStyle=ComboBoxStyle.DropDownList; zoom.Items.AddRange(new object[]{"50%","75%","100%","125%","150%","200%","300%"}); zoom.Width=110; zoom.Anchor=AnchorStyles.Left|AnchorStyles.Right; zoom.Margin=new Padding(0,0,0,6);
        gridCell.Minimum=8; gridCell.Maximum=128; gridCell.Increment=8; gridCell.Value=32; gridCell.Width=70; gridCell.Anchor=AnchorStyles.Left; gridCell.Margin=new Padding(0,0,0,6);
        areas.BorderStyle=BorderStyle.FixedSingle; areas.ItemHeight=26; areas.IntegralHeight=false; areas.Dock=DockStyle.Fill; areas.Margin=Padding.Empty; areas.MinimumSize=new Size(0,60);
        opacity.AutoSize=false; opacity.BackColor=Theme.Background; opacity.Height=30; opacity.Width=110; opacity.Anchor=AnchorStyles.Left|AnchorStyles.Right; opacity.Margin=new Padding(0,0,0,6);
        opacity.Minimum=20; opacity.Maximum=100; opacity.Value=100; opacity.TickFrequency=20;
        foreach(Control c in new Control[]{windows,zoom,gridCell,areas}) Theme.Style(c);

        var refresh=Theme.Button("Atualizar",false); refresh.Anchor=AnchorStyles.Left|AnchorStyles.Right; refresh.Width=90; refresh.Click+=delegate { RefreshWindows(); };
        var sourceRow=Table(Columns(AutoColumn(),PercentColumn(100),AutoColumn()),Theme.Caption("Janela do Tibia"),windows,refresh);

        var add=Theme.Button("Adicionar",true); edit=Theme.Button("Editar",false); var remove=Theme.Button("Remover",false);
        foreach(var b in new[]{add,edit,remove}) { b.Anchor=AnchorStyles.Left|AnchorStyles.Right; b.Margin=new Padding(0,0,4,6); }
        remove.Margin=new Padding(0,0,0,6);
        add.Click+=delegate { AddArea(); }; edit.Click+=delegate { EditArea(); }; remove.Click+=delegate { RemoveArea(); };
        tips.SetToolTip(add,"Adicionar área"); tips.SetToolTip(edit,"Editar área"); tips.SetToolTip(remove,"Remover área");
        var tools=Table(Columns(PercentColumn(34),PercentColumn(33),PercentColumn(33)),add,edit,remove);
        var left=Table(Columns(PercentColumn(100)),tools,areas);
        left.RowStyles[1]=new RowStyle(SizeType.Percent,100); left.Margin=new Padding(0,0,12,0);

        opacityLabel=Theme.Caption("Opacidade: 100%"); opacityLabel.MinimumSize=new Size(TextRenderer.MeasureText("Opacidade: 100%",Font).Width+2,0);
        var mapButton=Theme.Button("Definir mapa",false); mapButton.Anchor=AnchorStyles.Left|AnchorStyles.Right; mapButton.Click+=delegate { DefineMapArea(); };
        var gridCaption=Theme.Caption("Grade (px)");
        tips.SetToolTip(gridCaption,"Tamanho da célula da grade do mapa, em pixels"); tips.SetToolTip(gridCell,"Tamanho da célula da grade do mapa, em pixels"); tips.SetToolTip(mapButton,"Define a área do jogo onde a grade do mapa é desenhada");
        obsCapture=Theme.Button("Sincronizar com OBS",true); obsCapture.Anchor=AnchorStyles.Left|AnchorStyles.Right; obsCapture.Margin=new Padding(0,6,0,0); obsCapture.Click+=delegate { ToggleObsOutput(); };
        var fields=Table(Columns(AutoColumn(),PercentColumn(100)),Theme.Caption("Tamanho"),zoom,opacityLabel,opacity,gridCaption,gridCell);
        side=Table(Columns(PercentColumn(100)),fields,mapButton,obsCapture); side.Dock=DockStyle.Top; var right=side;
        var main=body=Table(Columns(PercentColumn(100),AutoColumn()),left,right);
        main.RowStyles[0]=new RowStyle(SizeType.Percent,100);

        detail=Theme.Note("Adicione uma área. Arraste os recortes para dentro do jogo.");
        tips.SetToolTip(obsCapture,"A Captura de jogo do Tibia fica direto na cena do OBS; as overlays viram grupos. Usa a imagem já capturada, não recaptura a tela.");
        obsStatus=Theme.Note("No OBS: Ferramentas > Scripts > adicione obs/TibiaScarabEye.lua.");
        var obs=obsStatus;

        mode=Theme.Button("Travar para jogar",true); var save=Theme.Button("Salvar layout",false); var load=Theme.Button("Abrir layout",false); visibility=Theme.Button("Ocultar overlays",false);
        mode.Click+=delegate { ToggleMode(); }; save.Click+=delegate { SaveLayout(); }; load.Click+=delegate { OpenLayout(); }; visibility.Click+=delegate { ToggleVisibility(); };
        tips.SetToolTip(mode,"Ctrl + Shift + F8, mesmo com o painel minimizado"); tips.SetToolTip(visibility,"Ctrl + Shift + F9, mesmo com o painel minimizado");
        foreach(var b in new[]{mode,save,load,visibility}) { b.Anchor=AnchorStyles.Left|AnchorStyles.Right; b.Margin=new Padding(0,0,6,0); }
        visibility.Margin=Padding.Empty;
        var actions=Table(Columns(PercentColumn(25),PercentColumn(25),PercentColumn(25),PercentColumn(25)),mode,save,load,visibility);
        status=Theme.Note("");

        var root=Table(Columns(PercentColumn(100)),sourceRow,main,detail,obs,actions,status);
        root.RowStyles[BodyRow]=new RowStyle(SizeType.Percent,100); layout=root;
        root.AutoSize=false; root.Padding=new Padding(16,0,16,12);
        foreach(Control c in new Control[]{sourceRow,main,detail,obs,actions}) c.Margin=new Padding(0,0,0,8);
        Controls.Add(root);
        ClientSize=new Size(660,512); MinimumSize=SizeFromClientSize(new Size(MinClientWidth,400));

        areas.SelectedIndexChanged+=delegate { BindSelection(); };
        areas.DoubleClick+=delegate { EditArea(); };
        gridCell.ValueChanged+=delegate { if(gridCell.Value<4) return; if(grid!=null) { grid.Close(); grid.Dispose(); grid=null; } dirty=true; if(!locked) RefreshGrid(); };
        zoom.SelectedIndexChanged+=delegate { if(binding || Selected==null || zoom.SelectedIndex<0) return; Safe(delegate { Selected.Zoom(new double[]{.5,.75,1,1.25,1.5,2,3}[zoom.SelectedIndex]); dirty=true; }); };
        opacity.ValueChanged+=delegate { opacityLabel.Text="Opacidade: "+opacity.Value+"%"; if(!binding && Selected!=null) { Selected.Spec.Opacity=opacity.Value; Selected.ApplyStyle(); dirty=true; } };
        windows.SelectedIndexChanged+=delegate {
            var item=windows.SelectedItem as WindowItem;
            if(item!=null && overlays.Count==0) { source=item.Handle; sourceTitle=item.Title; }
        };
        Shown+=delegate { RefreshWindows(); hotkey=Native.RegisterHotKey(Handle,1,0x4006,(uint)Keys.F8); visibilityHotkey=Native.RegisterHotKey(Handle,2,0x4006,(uint)Keys.F9); UpdateStatus(); };
        timer.Interval=750; timer.Tick+=delegate { TickSource(); }; timer.Start(); BindSelection();
    }
    // A altura mínima é achada no layout real na largura mínima (o texto quebra em mais linhas): cresce até o painel de áreas caber os controles ao lado.
    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        BeginInvoke(new Action(FitMinimumSize)); // depois do layout pendente, para medir o que o usuário realmente vê
    }
    void FitMinimumSize() {
        int minWidth=LogicalToDeviceUnits(MinClientWidth), width=ClientSize.Width, height=ClientSize.Height;
        int need=side.GetPreferredSize(Size.Empty).Height+body.Margin.Vertical, minHeight=LogicalToDeviceUnits(300);
        for(int pass=0;pass<5;pass++) {
            ClientSize=new Size(minWidth,minHeight); PerformLayout(); PerformLayout();
            int shortfall=need-layout.GetRowHeights()[BodyRow];
            if(shortfall<=0) break;
            minHeight+=shortfall;
        }
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
    Overlay Selected { get { return areas.SelectedIndex>=0 && areas.SelectedIndex<overlays.Count?overlays[areas.SelectedIndex]:null; } }
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
    // Shown in the title so a stale executable is obvious at a glance.
    static string BuildStamp() {
        try { return File.GetLastWriteTime(Application.ExecutablePath).ToString("dd/MM HH:mm"); } catch { return "?"; }
    }
    void StopObsOutput() {
        if(obsOutput!=null) { var output=obsOutput; obsOutput=null; output.Dispose(); }
        obsCapture.Text="Sincronizar com OBS";
        obsStatus.Text="Sincronização desligada. O grupo fica transparente no OBS.";
    }
    void Safe(Action action) { try { action(); } catch(Exception ex) { MessageBox.Show(this,ex.Message,"Tibia Scarab Eye",MessageBoxButtons.OK,MessageBoxIcon.Warning); } }
    void RefreshWindows() {
        if(overlays.Count>0) { MessageBox.Show(this,"Remova as áreas atuais ou abra um layout para trocar a janela de origem."); return; }
        windows.Items.Clear(); source=IntPtr.Zero;
        foreach(var item in Native.Windows()) windows.Items.Add(item);
        if(windows.Items.Count>0) {
            int selected=0;
            for(int i=0;i<windows.Items.Count;i++) if(((WindowItem)windows.Items[i]).Title.IndexOf("tibia",StringComparison.OrdinalIgnoreCase)>=0) { selected=i; break; }
            windows.SelectedIndex=selected;
        }
        UpdateStatus();
    }
    bool Ready() {
        if(!Native.IsWindow(source)) { MessageBox.Show(this,"Abra o Tibia e selecione sua janela. Se o jogo foi reiniciado, remova as áreas e clique em Atualizar."); return false; }
        if(Native.IsIconic(source)) { MessageBox.Show(this,"Restaure a janela de origem antes de continuar."); return false; }
        return true;
    }
    void AddArea() {
        if(!Ready()) return;
        if(overlays.Count>=30) { MessageBox.Show(this,"O protótipo permite até 30 áreas por layout."); return; }
        if(locked) ToggleMode();
        Safe(delegate {
            using(var select=new Selector(source)) if(select.ShowDialog(this)==DialogResult.OK) {
                var spec=select.Result; spec.Left=Screen.FromControl(this).WorkingArea.Left+40+overlays.Count*24; spec.Top=Screen.FromControl(this).WorkingArea.Top+40+overlays.Count*24;
                CreateOverlay(spec); dirty=true;
            }
        });
    }
    void CreateOverlay(RegionSpec spec) {
        var overlay=new Overlay(source,spec);
        overlay.SetObsMode(false);
        try { overlay.Show(); overlay.Render(); }
        catch { overlay.Dispose(); throw; }
        if(overlaysHidden) overlay.Hide();
        overlay.Changed+=delegate { dirty=true; };
        overlays.Add(overlay); areas.Items.Add(spec); areas.SelectedIndex=areas.Items.Count-1; windows.Enabled=false;
        detail.Text="Arraste o recorte para mover. Use as bordas para redimensionar.";
        obsCapture.Enabled=true;
    }
    void EditArea() {
        var overlay=Selected;
        if(overlay==null || !Ready()) return;
        Safe(delegate {
            using(var select=new Selector(source,overlay.Spec)) {
                if(select.ShowDialog(this)!=DialogResult.OK) return;
                overlay.UpdateRegion(select.Result);
                int index=overlays.IndexOf(overlay);
                areas.Items[index]=overlay.Spec; areas.SelectedIndex=index;
                dirty=true; BindSelection();
            }
        });
    }
    void DefineMapArea() {
        if(!Ready()) return;
        Safe(delegate {
            using(var select=new Selector(source,mapRegion)) if(select.ShowDialog(this)==DialogResult.OK) {
                mapRegion=select.Result; mapRegion.Name="Área do mapa"; dirty=true;
                if(grid!=null) { grid.Close(); grid.Dispose(); grid=null; }
                if(!locked) RefreshGrid();
            }
        });
    }
    Rectangle GridBounds {
        get {
            if(grid!=null && !grid.GridBounds.IsEmpty) return grid.GridBounds;
            var window=Native.ThumbnailBounds(source);
            if(mapRegion==null || window.Right<=window.Left || window.Bottom<=window.Top) return Rectangle.Empty;
            Rectangle r=Rectangle.FromLTRB(window.Left,window.Top,window.Right,window.Bottom);
            return Rectangle.FromLTRB(r.Left+(int)(mapRegion.X*r.Width),r.Top+(int)(mapRegion.Y*r.Height),r.Left+(int)((mapRegion.X+mapRegion.W)*r.Width),r.Top+(int)((mapRegion.Y+mapRegion.H)*r.Height));
        }
    }
    void RefreshGrid() {
        if(locked || overlaysHidden || mapRegion==null || !Native.IsWindow(source)) { if(grid!=null) grid.Hide(); return; }
        if(grid==null) grid=new GridOverlay(source,mapRegion,(int)gridCell.Value);
        grid.RefreshGrid();
        Rectangle bounds=GridBounds;
        foreach(var overlay in overlays) overlay.SnapToGrid(bounds,(int)gridCell.Value);
    }
    void ToggleVisibility() {
        overlaysHidden=!overlaysHidden;
        visibility.Text=overlaysHidden?"Mostrar overlays":"Ocultar overlays";
        TickSource(); UpdateStatus();
        if(!locked) RefreshGrid(); else if(grid!=null) grid.Hide();
    }
    void RemoveArea() {
        int index=areas.SelectedIndex; if(index<0) return;
        overlays[index].Close(); overlays[index].Dispose(); overlays.RemoveAt(index); areas.Items.RemoveAt(index); dirty=true;
        if(areas.Items.Count>0) areas.SelectedIndex=Math.Min(index,areas.Items.Count-1);
        windows.Enabled=overlays.Count==0;
        if(overlays.Count==0) { StopObsOutput(); locked=false; mode.Text="Travar para jogar"; detail.Text="Adicione uma área para começar."; }
        BindSelection();
    }
    void BindSelection() {
        binding=true; zoom.Enabled=opacity.Enabled=edit.Enabled=Selected!=null;
        obsCapture.Enabled=overlays.Count>0;
        zoom.SelectedIndex=-1;
        if(Selected!=null) opacity.Value=Selected.Spec.Opacity;
        binding=false;
    }
    void ToggleMode() {
        if(overlays.Count==0) return;
        Safe(delegate {
            locked=!locked;
            foreach(var overlay in overlays) overlay.SetLocked(locked);
            if(locked) { if(grid!=null) grid.Hide(); } else RefreshGrid();
            mode.Text=locked?"Destravar para editar":"Travar para jogar";
            detail.Text=locked?"Modo jogo: bordas fixas; cliques passam para a janela atrás.":"Arraste o recorte para mover. Use as bordas para redimensionar.";
        });
    }
    void UpdateStatus() {
        status.Text=hotkey?"Ctrl + Shift + F8: travar / editar"+(visibilityHotkey?" · F9: mostrar / ocultar":"")+".":"Atalho indisponível. Use o botão Travar para jogar.";
        if(overlaysHidden) status.Text="Overlays ocultas. Ctrl + Shift + F9 ou Mostrar overlays para restaurar.";
        if(!visibilityHotkey) status.Text+="\nF9 indisponível: use o botão Mostrar/Ocultar overlays.";
        if(source==IntPtr.Zero) status.Text="Nenhuma janela selecionada. Abra o jogo e clique em Atualizar.";
    }
    void TickSource() {
        if(overlays.Count==0) return;
        bool alive=Native.IsWindow(source), visible=alive && !Native.IsIconic(source) && !overlaysHidden;
        foreach(var overlay in overlays) {
            if(visible && !overlay.Visible) overlay.Show();
            if(!visible && overlay.Visible) overlay.Hide();
            if(visible) { try { overlay.Render(); } catch { visible=false; overlay.Hide(); } }
        }
        if(visible && !locked) RefreshGrid(); else if(grid!=null) grid.Hide();
        if(!alive) status.Text="A janela de origem foi fechada. Salve o layout; remova as áreas e selecione o jogo novamente.";
        else if(!visible && !overlaysHidden) status.Text="Recortes pausados. Restaure a janela de origem para continuar.";
        else UpdateStatus();
    }
    bool SaveLayout() {
        if(overlays.Count==0) return true;
        using(var dialog=new SaveFileDialog { Filter="Layout Tibia Scarab Eye (*.json)|*.json", FileName="Meu layout.json", Title="Salvar layout" }) {
            if(dialog.ShowDialog(this)!=DialogResult.OK) return false;
            try {
                var layout=new Layout { SourceTitle=sourceTitle,MapRegion=mapRegion,GridCellPixels=(int)gridCell.Value };
                foreach(var overlay in overlays) { overlay.CaptureSpec(); layout.Regions.Add(overlay.Spec); }
                layout.Save(dialog.FileName); dirty=false; status.Text="Layout salvo em "+dialog.FileName; return true;
            } catch(Exception ex) { MessageBox.Show(this,"Não foi possível salvar.\n"+ex.Message); return false; }
        }
    }
    bool CanReplace() {
        if(!dirty || overlays.Count==0) return true;
        var result=MessageBox.Show(this,"Salvar o layout atual antes de continuar?","Layout com alterações",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
        return result==DialogResult.No || (result==DialogResult.Yes && SaveLayout());
    }
    void ClearOverlays() { StopObsOutput(); if(grid!=null) { grid.Close(); grid.Dispose(); grid=null; } foreach(var overlay in overlays) { overlay.Close(); overlay.Dispose(); } overlays.Clear(); areas.Items.Clear(); windows.Enabled=true; locked=false; mode.Text="Travar para jogar"; }
    void OpenLayout() {
        if(!Ready()) return;
        using(var dialog=new OpenFileDialog { Filter="Layout Tibia Scarab Eye (*.json)|*.json",Title="Abrir layout na janela selecionada" }) {
            if(dialog.ShowDialog(this)!=DialogResult.OK) return;
            Safe(delegate {
                var layout=Layouts.Layout.Load(dialog.FileName);
                if(!CanReplace()) return;
                ClearOverlays();
                mapRegion=layout.MapRegion; gridCell.Value=Math.Max(gridCell.Minimum,Math.Min(gridCell.Maximum,layout.GridCellPixels));
                try { foreach(var spec in layout.Regions) CreateOverlay(spec); dirty=false; }
                catch { dirty=overlays.Count>0; throw; }
                status.Text="Layout aberto na janela selecionada. Confira os recortes se a resolução mudou.";
            });
        }
    }
    protected override void WndProc(ref Message m) { if(m.Msg==0x312) { if(m.WParam.ToInt32()==1) { ToggleMode(); return; } if(m.WParam.ToInt32()==2) { ToggleVisibility(); return; } } base.WndProc(ref m); }
    protected override void OnFormClosing(FormClosingEventArgs e) { if(e.CloseReason==CloseReason.UserClosing && !CanReplace()) { e.Cancel=true; return; } timer.Stop(); ClearOverlays(); Native.UnregisterHotKey(Handle,1); Native.UnregisterHotKey(Handle,2); base.OnFormClosing(e); }
    protected override void Dispose(bool disposing) { if(disposing) { timer.Dispose(); tips.Dispose(); } base.Dispose(disposing); }
}
