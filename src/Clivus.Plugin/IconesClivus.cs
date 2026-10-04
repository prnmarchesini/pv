using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// Os ícones da identidade visual do Clivus Solar (04/10/2026), gerados por
/// tools/build_icons.py e embutidos na DLL como recurso WPF
/// (Resources/Icons/Light|Dark). O tema vem da variável COLORTHEME do AutoCAD
/// (0 escuro, 1 claro); com a tela acima de 100% de escala, a versão @2x.
/// Sem AdWindows aqui: a ribbon (<see cref="RibbonClivus"/>) é quem usa.
/// </summary>
internal static class IconesClivus
{
    private static readonly Dictionary<string, ImageSource?> Cache = [];

    /// <summary>Se o AutoCAD está no tema escuro (COLORTHEME = 0).</summary>
    internal static bool TemaEscuro
    {
        get
        {
            try
            {
                return Convert.ToInt32(AcadApp.GetSystemVariable("COLORTHEME"), System.Globalization.CultureInfo.InvariantCulture) == 0;
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Não consegui ler COLORTHEME; uso o tema escuro.", erro);
                return true;
            }
        }
    }

    /// <summary>A escala da tela acima de 100%: usa a versão @2x.</summary>
    private static bool TelaDensa
    {
        get
        {
            try
            {
                return GetDpiForSystem() > 96;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }

    /// <summary>O ícone de 16 px (botão pequeno, item de menu).</summary>
    internal static ImageSource? Pequeno(string nome) => Carregar(nome, 16);

    /// <summary>O ícone de 32 px (botão grande).</summary>
    internal static ImageSource? Grande(string nome) => Carregar(nome, 32);

    private static ImageSource? Carregar(string nome, int tamanho)
    {
        var tema = TemaEscuro ? "Dark" : "Light";
        var arquivo = $"{nome}_{tamanho}{(TelaDensa ? "@2x" : string.Empty)}.png";
        var chave = tema + "/" + arquivo;

        lock (Cache)
        {
            if (Cache.TryGetValue(chave, out var guardado)) return guardado;

            ImageSource? imagem = null;

            try
            {
                // Garante o esquema pack:// registrado (o AutoCAD nem sempre
                // carregou o WPF de aplicação antes da ribbon).
                _ = System.IO.Packaging.PackUriHelper.UriSchemePack;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri($"pack://application:,,,/Clivus.Plugin;component/Resources/Icons/{tema}/{arquivo}", UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                imagem = bitmap;
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar($"Não achei o ícone {chave}.", erro);
            }

            Cache[chave] = imagem;
            return imagem;
        }
    }

    /// <summary>Uma imagem da marca (Resources/Branding), ou null.</summary>
    internal static ImageSource? Marca(string arquivo)
    {
        try
        {
            _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
            var bitmap = new BitmapImage(new Uri($"pack://application:,,,/Clivus.Plugin;component/Resources/Branding/{arquivo}", UriKind.Absolute));
            bitmap.Freeze();
            return bitmap;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar($"Não achei a imagem da marca {arquivo}.", erro);
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}
