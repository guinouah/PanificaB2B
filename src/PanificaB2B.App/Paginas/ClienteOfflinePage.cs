using PanificaB2B.App.Dados;
using PanificaB2B.Contratos;

namespace PanificaB2B.App.Paginas;

public sealed class ClienteOfflinePage : ContentPage
{
    readonly PreparacaoDto dados;
    readonly TrabalhoLocal local;
    readonly ClienteEntrada entrada;
    readonly Entry nome = new() { Placeholder = "Nome / razão social", MaxLength = 150 };
    readonly Entry documento = new() { Placeholder = "Documento", MaxLength = 30 };
    readonly Entry email = new() { Placeholder = "E-mail", Keyboard = Keyboard.Email, MaxLength = 150 };
    readonly Entry telefone = new() { Placeholder = "Telefone", Keyboard = Keyboard.Telephone, MaxLength = 40 };
    readonly Entry endereco = new() { Placeholder = "Endereço", MaxLength = 250 };
    readonly Picker territorio = new() { Title = "Território" };
    readonly Picker cidade = new() { Title = "Cidade" };
    List<CidadeDto> cidades = [];
    public ClienteOfflinePage(PreparacaoDto dados, TrabalhoLocal local, ClienteEntrada? cliente = null)
    {
        this.dados = dados; this.local = local; entrada = cliente ?? new() { Id = Guid.NewGuid() };
        Title = "Cliente offline";
        nome.Text = entrada.Nome; documento.Text = entrada.Documento; email.Text = entrada.Email; telefone.Text = entrada.Telefone; endereco.Text = entrada.Endereco;
        foreach (var t in dados.Territorios) territorio.Items.Add(t.Nome);
        territorio.SelectedIndexChanged += (_, _) => Cidades();
        territorio.SelectedIndex = cliente is null ? (territorio.Items.Count > 0 ? 0 : -1) : dados.Territorios.FindIndex(x => x.Id == cliente.TerritorioId);
        Cidades();
        if (cliente is not null) cidade.SelectedIndex = cidades.FindIndex(x => x.Id == cliente.CidadeId);
        var corpo=TemaVenda.Corpo();
        corpo.Children.Add(TemaVenda.Cabecalho("CARTEIRA","Cadastro de cliente","Registre uma nova panificadora durante a visita, mesmo sem conexão."));
        corpo.Children.Add(TemaVenda.Cartao(TemaVenda.Titulo("Dados do estabelecimento",18),TemaVenda.Campo("Nome / razão social",nome),TemaVenda.Campo("Documento (opcional)",documento),TemaVenda.Campo("E-mail (opcional)",email),TemaVenda.Campo("Telefone (opcional)",telefone),TemaVenda.Campo("Endereço",endereco)));
        corpo.Children.Add(TemaVenda.Cartao(TemaVenda.Titulo("Região de atendimento",18),TemaVenda.Campo("Território",territorio),TemaVenda.Campo("Cidade / UF",cidade)));
        corpo.Children.Add(TemaVenda.Legenda("O cadastro fica no aparelho e será enviado antes dos pedidos vinculados a este cliente."));
        corpo.Children.Add(InterfaceVenda.Botao("Salvar cliente no aparelho →",Salvar));
        Content=TemaVenda.Pagina(corpo);
    }
    void Cidades()
    {
        cidade.Items.Clear(); cidades = [];
        if (territorio.SelectedIndex < 0) return;
        var ids = dados.Territorios[territorio.SelectedIndex].CidadeIds;
        cidades = dados.Cidades.Where(x => x.Ativo && ids.Contains(x.Id)).ToList();
        foreach (var c in cidades) cidade.Items.Add($"{c.Nome} · {dados.Estados.FirstOrDefault(e=>e.Id==c.EstadoId)?.Sigla}");
        if (cidade.Items.Count > 0) cidade.SelectedIndex = 0;
    }
    async Task Salvar()
    {
        if (string.IsNullOrWhiteSpace(nome.Text) || string.IsNullOrWhiteSpace(endereco.Text) || territorio.SelectedIndex < 0 || cidade.SelectedIndex < 0) throw new InvalidOperationException("Preencha nome, endereço, território e cidade.");
        entrada.Nome = nome.Text.Trim(); entrada.Documento = documento.Text?.Trim(); entrada.Email = email.Text?.Trim(); entrada.Telefone = telefone.Text?.Trim(); entrada.Endereco = endereco.Text.Trim();
        entrada.TerritorioId = dados.Territorios[territorio.SelectedIndex].Id; entrada.CidadeId = cidades[cidade.SelectedIndex].Id;
        if (!new EmailOpcionalAttribute().IsValid(entrada.Email)) throw new InvalidOperationException("Informe um e-mail válido.");
        await local.SalvarCliente(dados.VendedorId, entrada);
        await DisplayAlertAsync("Salvo", "Cliente salvo no dispositivo. Já é possível montar um pedido para ele.", "OK");
        await Navigation.PopAsync();
    }
}
