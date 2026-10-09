using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using TibiaScarabEye.Interop;
using TibiaScarabEye.Layouts;
using TibiaScarabEye.Obs;

namespace TibiaScarabEye.UI;

internal sealed class MainForm : Form {
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
    public MainForm() {
        Theme.Apply(this); Text="Tibia Scarab Eye • Organize sua visão de jogo • build "+BuildStamp();
        ClientSize=new Size(740,752); MinimumSize=Size; StartPosition=FormStartPosition.CenterScreen;
        var title=Theme.Label("Tibia Scarab Eye",28,20,600,36,false); title.Font=new Font("Tahoma",21,FontStyle.Bold); Controls.Add(title);
        Controls.Add(Theme.Label("Organize seus recortes. Leve o grupo inteiro para o OBS.",28,62,680,24,true));
        Controls.Add(Theme.Label("Janela do Tibia",28,98,500,22,false));
        windows.SetBounds(28,127,530,30); windows.DropDownStyle=ComboBoxStyle.DropDownList; Controls.Add(windows);
        var refresh=Theme.Button("Atualizar",570,121,140,false); refresh.Click+=delegate { RefreshWindows(); }; Controls.Add(refresh);
        var add=Theme.Button("Adicionar área",28,180,165,true); add.Click+=delegate { AddArea(); }; Controls.Add(add);
        edit=Theme.Button("Editar área",205,180,145,false); edit.Click+=delegate { EditArea(); }; Controls.Add(edit);
        var remove=Theme.Button("Remover área",362,180,140,false); remove.Click+=delegate { RemoveArea(); }; Controls.Add(remove);
        var mapButton=Theme.Button("Definir mapa",419,394,180,false); mapButton.Click+=delegate { DefineMapArea(); }; Controls.Add(mapButton);
        Controls.Add(Theme.Label("Grade do mapa / célula em px",419,369,290,22,true));
        gridCell.SetBounds(615,400,94,30); gridCell.Minimum=8; gridCell.Maximum=128; gridCell.Increment=8; gridCell.Value=32; gridCell.BackColor=Color.FromArgb(39,39,37); gridCell.ForeColor=Theme.Ink; Controls.Add(gridCell);
        gridCell.ValueChanged+=delegate { if(gridCell.Value<4) return; if(grid!=null) { grid.Close(); grid.Dispose(); grid=null; } dirty=true; if(!locked) RefreshGrid(); };
        areas.SetBounds(28,237,365,195); areas.BackColor=Theme.Surface; areas.ForeColor=Theme.Ink; areas.BorderStyle=BorderStyle.FixedSingle; areas.ItemHeight=32; areas.IntegralHeight=false;
        areas.SelectedIndexChanged+=delegate { BindSelection(); }; Controls.Add(areas);
        areas.DoubleClick+=delegate { EditArea(); };
        Controls.Add(Theme.Label("Tamanho do recorte",419,237,290,25,false));
        zoom.SetBounds(419,265,290,30); zoom.DropDownStyle=ComboBoxStyle.DropDownList; zoom.Items.AddRange(new object[]{"50%","75%","100%","125%","150%","200%","300%"});
        zoom.SelectedIndexChanged+=delegate { if(binding || Selected==null || zoom.SelectedIndex<0) return; Safe(delegate { Selected.Zoom(new double[]{.5,.75,1,1.25,1.5,2,3}[zoom.SelectedIndex]); dirty=true; }); }; Controls.Add(zoom);
        opacityLabel=Theme.Label("Opacidade: 100%",419,306,290,24,true); Controls.Add(opacityLabel);
        opacity.AutoSize=false; opacity.SetBounds(413,330,300,32); opacity.Minimum=20; opacity.Maximum=100; opacity.Value=100; opacity.TickFrequency=20;
        opacity.ValueChanged+=delegate { opacityLabel.Text="Opacidade: "+opacity.Value+"%"; if(!binding && Selected!=null) { Selected.Spec.Opacity=opacity.Value; Selected.ApplyStyle(); dirty=true; } }; Controls.Add(opacity);
        detail=Theme.Label("Adicione uma área. Arraste os recortes para dentro do jogo.",28,446,680,26,true); Controls.Add(detail);
        Controls.Add(Theme.Label("Grupo de overlays no OBS",28,488,470,24,false));
        obsCapture=Theme.Button("Sincronizar com OBS",514,482,196,true); obsCapture.Click+=delegate { ToggleObsOutput(); }; Controls.Add(obsCapture);
        Controls.Add(Theme.Label("No OBS: Ferramentas > Scripts > adicione obs/TibiaScarabEye.lua.\nA Captura de jogo do Tibia fica direto na cena; as overlays viram grupos.",28,527,680,40,true));
        obsStatus=Theme.Label("Usa a imagem já capturada pelo OBS. Não recaptura a tela.",28,572,680,30,true); Controls.Add(obsStatus);
        mode=Theme.Button("Travar para jogar",28,616,230,true); mode.Click+=delegate { ToggleMode(); }; Controls.Add(mode);
        var save=Theme.Button("Salvar layout",274,616,190,false); save.Click+=delegate { SaveLayout(); }; Controls.Add(save);
        var load=Theme.Button("Abrir layout",480,616,230,false); load.Click+=delegate { OpenLayout(); }; Controls.Add(load);
        visibility=Theme.Button("Ocultar overlays",28,666,230,false); visibility.Click+=delegate { ToggleVisibility(); }; Controls.Add(visibility);
        Controls.Add(Theme.Label("Ctrl + Shift + F9: mostrar / ocultar",274,675,430,24,true));
        status=Theme.Label("",28,716,680,32,true); Controls.Add(status);
        windows.SelectedIndexChanged+=delegate {
            var item=windows.SelectedItem as WindowItem;
            if(item!=null && overlays.Count==0) { source=item.Handle; sourceTitle=item.Title; }
        };
        Shown+=delegate { RefreshWindows(); hotkey=Native.RegisterHotKey(Handle,1,0x4006,(uint)Keys.F8); visibilityHotkey=Native.RegisterHotKey(Handle,2,0x4006,(uint)Keys.F9); UpdateStatus(); };
        timer.Interval=750; timer.Tick+=delegate { TickSource(); }; timer.Start(); BindSelection();
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
        status.Text=hotkey?"Ctrl + Shift + F8: travar / editar, mesmo com o painel minimizado.":"Atalho indisponível. Use o botão Travar para jogar.";
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
    protected override void Dispose(bool disposing) { if(disposing) timer.Dispose(); base.Dispose(disposing); }
}
