namespace TibiaScarabEye.Alerts;

// Os itens de duração que o programa acompanha. Os dois duram 30 minutos equipados (Tibia Wiki).
public enum PlasmaItem { Collar, Ring }

internal static class PlasmaInfo {
    public static readonly PlasmaItem[] All={PlasmaItem.Collar,PlasmaItem.Ring};
    public static string Name(PlasmaItem item) { return item==PlasmaItem.Collar?"Collar of Red Plasma":"Ring of Red Plasma"; }
    // Nome do recurso embutido (Assets\Sprites): o mesmo sprite que aparece na janela e que é procurado nas áreas recortadas.
    public static string SpriteResource(PlasmaItem item) { return "Sprites."+(item==PlasmaItem.Collar?"Collar_of_Red_Plasma":"Ring_of_Red_Plasma")+".gif"; }
    // Prefixo dos arquivos de som: collar-on.wav, collar-warning.wav, collar-off.wav (e o mesmo com ring).
    public static string SoundPrefix(PlasmaItem item) { return item==PlasmaItem.Collar?"collar":"ring"; }
}
