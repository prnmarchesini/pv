namespace UFV.Core;

/// <summary>
/// A pasta onde os perfis de mesa moram, e as quatro coisas que se faz com
/// eles: listar, ler, gravar e apagar.
///
/// A pasta vem de fora de propósito: quem sabe onde guardar é o plugin, que
/// conhece o perfil do usuário no Windows. O Core sabe o que é um perfil de
/// mesa; onde ele fica no disco não é assunto dele.
///
/// O nome do arquivo sai do nome do perfil, e é aí que mora o cuidado: o nome
/// é texto livre digitado pelo projetista, e "Mesa 2V / 28 módulos" tem uma
/// barra no meio. Sem tratar isso, gravar o perfil tentaria criar uma pasta
/// chamada "Mesa 2V " — ou, pior num caminho montado sem cuidado, escrever
/// fora da pasta dos perfis.
/// </summary>
public sealed class TableProfileStore
{
    /// <summary>Extensão dos arquivos de perfil.</summary>
    public const string Extensao = ".ufvmesa.json";

    /// <summary>
    /// Maior nome de perfil aceito.
    ///
    /// O Windows corta o caminho inteiro em 260 caracteres por padrão, e o
    /// nome do perfil é só um pedaço dele. Cento e vinte deixa folga para a
    /// pasta e para a extensão, e ainda é nome demais para qualquer mesa.
    /// </summary>
    public const int MaiorNome = 120;

    /// <summary>
    /// Maior nome de arquivo depois do escape.
    ///
    /// Cada caractere convertido ocupa cinco, então o limite do nome não
    /// garante o limite do arquivo — e é o do arquivo que o Windows cobra.
    /// </summary>
    private const int MaiorArquivo = 180;

    private readonly string _pasta;

    /// <param name="pasta">A pasta onde os perfis ficam. É criada se não existir.</param>
    public TableProfileStore(string pasta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pasta);

        // Sem a barra do fim: com ela, GetDirectoryName do caminho montado
        // nunca bate com a pasta, e a conferência de segurança passaria a
        // recusar TODO nome — culpando o nome pelo erro de quem montou o
        // caminho.
        _pasta = Path.TrimEndingDirectorySeparator(Path.GetFullPath(pasta));
    }

    /// <summary>A pasta onde os perfis ficam.</summary>
    public string Folder => _pasta;

    /// <summary>
    /// Os nomes dos perfis guardados, em ordem alfabética.
    ///
    /// Pasta que não existe devolve lista vazia, e não erro: antes do primeiro
    /// perfil salvo é exatamente esse o estado, e ele é normal.
    /// </summary>
    public IReadOnlyList<string> List()
    {
        if (!Directory.Exists(_pasta)) return [];

        return Directory
            .EnumerateFiles(_pasta, "*" + Extensao)
            .Select(caminho => Path.GetFileName(caminho)[..^Extensao.Length])
            .Select(Desescapar)
            // Invariante, e não da cultura da máquina, pelo mesmo motivo da
            // biblioteca de módulos: dois projetistas têm que ver a mesma
            // lista na mesma ordem.
            .OrderBy(nome => nome, StringComparer.InvariantCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Se existe um perfil com este nome.</summary>
    public bool Exists(string nome) => File.Exists(Caminho(nome));

    /// <summary>
    /// Grava o perfil, substituindo o que houver com o mesmo nome.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se o perfil descreve uma mesa que não existe, ou se o nome não serve
    /// para um arquivo.
    /// </exception>
    public void Save(TableProfile perfil)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        // ToJson recusa perfil inválido; chamar antes de mexer no disco evita
        // deixar um arquivo pela metade.
        var json = perfil.ToJson();
        var caminho = Caminho(perfil.Name);

        ConferirColisao(perfil.Name, caminho);

        Directory.CreateDirectory(_pasta);

        // Grava ao lado e troca no fim: se faltar energia no meio da escrita,
        // o perfil antigo continua inteiro em vez de virar um arquivo cortado
        // que não abre mais.
        //
        // O nome do provisório carrega processo e sorteio: com um nome fixo,
        // dois Civil 3D salvando o mesmo perfil brigavam pelo mesmo arquivo —
        // e na pior janela um publicava o arquivo pela metade do outro por
        // cima do perfil bom, que é justamente o que este esquema existia para
        // impedir.
        var provisorio = $"{caminho}.{Environment.ProcessId}.{Guid.NewGuid():N}.gravando";

        try
        {
            File.WriteAllText(provisorio, json, System.Text.Encoding.UTF8);
            Trocar(provisorio, caminho);
        }
        catch
        {
            // Sem isto, um Move que falha deixa o provisório na pasta para
            // sempre. List() o ignora, e ninguém nunca o limpa.
            TentarApagar(provisorio);
            throw;
        }
    }

    /// <summary>
    /// O perfil com este nome.
    /// </summary>
    /// <exception cref="FileNotFoundException">Se não houver perfil com este nome.</exception>
    /// <exception cref="InvalidOperationException">Se o arquivo não for um perfil legível.</exception>
    public TableProfile Load(string nome)
    {
        var caminho = Caminho(nome);

        if (!File.Exists(caminho))
            throw new FileNotFoundException($"Não há perfil de mesa chamado \"{nome}\".", caminho);

        return TableProfile.Parse(File.ReadAllText(caminho, System.Text.Encoding.UTF8));
    }

    /// <summary>Apaga o perfil, e devolve se havia algo para apagar.</summary>
    public bool Delete(string nome)
    {
        var caminho = Caminho(nome);

        if (!File.Exists(caminho)) return false;

        File.Delete(caminho);
        return true;
    }

    /// <summary>
    /// O caminho do arquivo deste perfil.
    /// </summary>
    /// <exception cref="InvalidOperationException">Se o nome não serve para um arquivo.</exception>
    private string Caminho(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new InvalidOperationException("O perfil de mesa precisa de um nome.");

        // Sem Trim: " Mesa " e "Mesa" são nomes diferentes, e trimar aqui
        // faria um sobrescrever o arquivo do outro enquanto a listagem
        // mostrava só um deles. Quem garante que o nome cabe num arquivo é o
        // escape, que trata inclusive espaço nas pontas.
        var limpo = nome;

        if (limpo.Length > MaiorNome)
        {
            throw new InvalidOperationException(
                $"O nome do perfil tem {limpo.Length} caracteres, e o limite é {MaiorNome}.");
        }

        var escapado = Escapar(limpo);

        // O limite vale para o nome JÁ escapado: um nome de 120 barras passa
        // pelo limite de cima e vira um arquivo de 600 caracteres, que estoura
        // o caminho do Windows com um erro que não diz nada ao projetista.
        if (escapado.Length > MaiorArquivo)
        {
            throw new InvalidOperationException(
                $"O nome \"{limpo}\" tem caracteres demais que precisam ser convertidos "
                + "para virar nome de arquivo. Use um nome mais simples.");
        }

        var arquivo = escapado + Extensao;
        var caminho = Path.GetFullPath(Path.Combine(_pasta, arquivo));

        // Cinto e suspensório. O escape já tira separador de caminho e ponto,
        // mas uma falha nele não pode virar escrita fora da pasta dos perfis.
        if (!string.Equals(Path.GetDirectoryName(caminho), _pasta, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"O nome \"{limpo}\" não serve para um arquivo de perfil.");
        }

        return caminho;
    }

    /// <summary>
    /// Recusa gravar por cima de um perfil que só difere na caixa.
    ///
    /// O sistema de arquivos do Windows não distingue maiúscula de minúscula;
    /// o escape distingue. Sem esta conferência, salvar "mesa" apagava "Mesa"
    /// em silêncio, e pedir "Mesa" de volta devolvia "mesa" — o projetista
    /// perdia um perfil sem nenhum aviso.
    /// </summary>
    private void ConferirColisao(string nome, string caminho)
    {
        if (!Directory.Exists(_pasta)) return;

        var alvo = Path.GetFileName(caminho);

        foreach (var existente in Directory.EnumerateFiles(_pasta, "*" + Extensao))
        {
            var arquivo = Path.GetFileName(existente);

            // Mesmo arquivo: é substituição legítima do próprio perfil.
            if (string.Equals(arquivo, alvo, StringComparison.Ordinal)) continue;
            if (!string.Equals(arquivo, alvo, StringComparison.OrdinalIgnoreCase)) continue;

            var outro = Desescapar(arquivo[..^Extensao.Length]);

            throw new InvalidOperationException(
                $"Já existe um perfil chamado \"{outro}\", que difere de \"{nome}\" só em "
                + "maiúsculas e minúsculas. O Windows não distingue os dois: escolha outro nome.");
        }
    }

    /// <summary>
    /// Põe o provisório no lugar do definitivo, insistindo um pouco.
    ///
    /// Dois Civil 3D salvando o mesmo perfil no mesmo instante disputam o
    /// arquivo de destino, e o Windows devolve acesso negado a quem perder —
    /// mesmo com cada um escrevendo no seu próprio provisório. Não é corrupção:
    /// é a janela de milissegundos em que o outro ainda está trocando.
    ///
    /// Insistir três vezes com uma pausa curta resolve na prática e não esconde
    /// erro de verdade: uma pasta sem permissão de escrita falha nas três e a
    /// exceção sobe igual.
    /// </summary>
    private static void Trocar(string provisorio, string caminho)
    {
        // Prazo, e não contagem fixa de tentativas. Com três tentativas de 20 e
        // 40 ms, o teste de gravação concorrente falhava de vez em quando — e
        // teste instável é pior que teste nenhum, porque ensina a ignorar o
        // placar. Um segundo e meio é muito mais do que a disputa real entre
        // dois Civil 3D precisa, e continua curto o bastante para uma pasta
        // sem permissão falhar rápido em vez de travar a janela.
        var prazo = DateTime.UtcNow.AddSeconds(1.5);
        var espera = 5;

        while (true)
        {
            try
            {
                File.Move(provisorio, caminho, overwrite: true);
                return;
            }
            catch (Exception erro) when (
                DateTime.UtcNow < prazo && erro is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(espera);
                espera = Math.Min(espera * 2, 60);
            }
        }
    }

    private static void TentarApagar(string caminho)
    {
        try
        {
            if (File.Exists(caminho)) File.Delete(caminho);
        }
        catch (IOException)
        {
            // Não há o que fazer, e falhar a limpeza não pode esconder o erro
            // de verdade que trouxe o código até aqui.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// O nome do perfil como nome de arquivo.
    ///
    /// Cada caractere proibido vira <c>%</c> seguido do código dele em
    /// hexadecimal, que é reversível — assim o nome volta inteiro na listagem,
    /// com barra, acento e o que mais o projetista tiver digitado. Trocar por
    /// sublinhado seria mais simples e faria "Mesa 2V/28" e "Mesa 2V-28"
    /// virarem o mesmo arquivo, um apagando o outro em silêncio.
    /// </summary>
    private static string Escapar(string nome)
    {
        var proibidos = Path.GetInvalidFileNameChars();
        var texto = new System.Text.StringBuilder(nome.Length + 8);

        foreach (var letra in nome)
        {
            // Espaço nas pontas também é escapado: o Windows corta espaço no
            // fim do nome de arquivo sem avisar, e aí " Mesa " e "Mesa "
            // virariam o mesmo arquivo.
            var naPonta = texto.Length == 0 || texto.Length == nome.Length - 1;

            // O % também é escapado, senão um nome que já o contenha poderia
            // se confundir com um escape nosso na volta.
            if (letra == '%' || letra == '.'
                || (letra == ' ' && naPonta)
                || Array.IndexOf(proibidos, letra) >= 0)
            {
                texto.Append('%').Append(((int)letra).ToString("X4"));
            }
            else
            {
                texto.Append(letra);
            }
        }

        return texto.ToString();
    }

    private static string Desescapar(string arquivo)
    {
        if (!arquivo.Contains('%')) return arquivo;

        var texto = new System.Text.StringBuilder(arquivo.Length);

        for (var i = 0; i < arquivo.Length; i++)
        {
            if (arquivo[i] == '%' && i + 4 < arquivo.Length
                && int.TryParse(
                    arquivo.AsSpan(i + 1, 4),
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var codigo))
            {
                texto.Append((char)codigo);
                i += 4;
            }
            else
            {
                texto.Append(arquivo[i]);
            }
        }

        return texto.ToString();
    }
}
