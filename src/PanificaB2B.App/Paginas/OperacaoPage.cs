using System.Globalization;
using System.Net.Http.Json;
using PanificaB2B.App.Dados;
using PanificaB2B.Contratos;

namespace PanificaB2B.App.Paginas;

public static class InterfaceVenda
{
    public static Label Texto(string texto, bool destaque = false) => new() { Text = texto, FontSize = destaque ? 20 : 15, FontAttributes = destaque ? FontAttributes.Bold : FontAttributes.None };
    public static Button Botao(string texto, Func<Task> acao)
    {
        var button = new Button { Text = texto };
        button.Clicked += async (_, _) =>
        {
            button.IsEnabled = false;
            try { await acao(); }
            catch (Exception ex) { if (button.Window?.Page is { } page) await page.DisplayAlertAsync("Confira os dados", ex is HttpRequestException ? "Confira a conexão e entre novamente se necessário." : ex.Message, "OK"); }
            finally { button.IsEnabled = true; }
        };
        return button;
    }
    public static decimal Numero(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return 0;
        if (!decimal.TryParse(texto.Trim().Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor) || valor < 0 || decimal.Round(valor, 3) != valor) throw new InvalidOperationException("Use quantidades positivas com até três casas decimais.");
        return valor;
    }
    public static string Dinheiro(decimal valor) => valor.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
    public static readonly string[] Dias = ["Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado"];
}

public sealed class OperacaoPage : ContentPage
{
    readonly SessaoDispositivo sessao;
    readonly CarteiraLocal cache;
    readonly TrabalhoLocal trabalho;
    readonly VerticalStackLayout lista = new() { Spacing = 12 };
    readonly Picker roteiro = new() { Title = "Roteiro" };
    PreparacaoDto dados;
    public OperacaoPage(SessaoDispositivo sessao, PreparacaoDto dados, CarteiraLocal cache, TrabalhoLocal trabalho)
    {
        this.sessao = sessao; this.dados = dados; this.cache = cache; this.trabalho = trabalho;
        Title = "Vendas do dia";
        roteiro.Items.Add("Toda a carteira");
        foreach (var r in dados.Roteiros) roteiro.Items.Add($"{InterfaceVenda.Dias[r.DiaSemana]} · {r.Nome}");
        roteiro.SelectedIndex = 0; roteiro.SelectedIndexChanged += async (_, _) => await Atualizar();
        var corpo = new VerticalStackLayout { Padding = 20, Spacing = 14, Children = {
            InterfaceVenda.Texto("Carteira e visitas", true),
            InterfaceVenda.Texto($"Dados de {dados.AtualizadoEm.ToLocalTime():dd/MM/yyyy HH:mm}. Dívida e preços refletem esse download."),
            InterfaceVenda.Botao("Cadastrar cliente offline", () => Navigation.PushAsync(new ClienteOfflinePage(dados, trabalho))),
            InterfaceVenda.Botao("Catálogo, promoções e novidades", Catalogo),
            InterfaceVenda.Botao("Pedidos locais e envio (12h / 19h)", () => Navigation.PushAsync(new PendenciasPage(sessao, dados, trabalho))),
            roteiro, lista
        }};
        Content = new ScrollView { Content = corpo };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { dados = await cache.Carregar(dados.VendedorId) ?? dados; await Atualizar(); }
        catch (Exception ex) { await DisplayAlertAsync("Carteira", ex.Message, "OK"); }
    }
    async Task Atualizar()
    {
        lista.Children.Clear();
        var clientes = TrabalhoLocal.Clientes(dados, await trabalho.Listar(dados.VendedorId));
        var visitas = roteiro.SelectedIndex > 0 && roteiro.SelectedIndex <= dados.Roteiros.Count ? dados.Roteiros[roteiro.SelectedIndex - 1].Visitas : null;
        if (visitas is not null) clientes = visitas.OrderBy(x => x.Sequencia).Select(v => clientes.FirstOrDefault(x => x.Id == v.ClienteId)).OfType<ClienteDto>().ToList();
        foreach (var cliente in clientes)
        {
            var sequencia = visitas?.FirstOrDefault(x => x.ClienteId == cliente.Id)?.Sequencia;
            var divida = dados.Dividas.FirstOrDefault(x => x.ClienteId == cliente.Id);
            lista.Children.Add(InterfaceVenda.Botao($"{(sequencia is null ? "" : sequencia + " · ")}{cliente.Nome}\nEm aberto: {InterfaceVenda.Dinheiro(divida?.PrincipalAberto ?? 0)}", () => AbrirCliente(cliente)));
        }
        if (clientes.Count == 0) lista.Children.Add(InterfaceVenda.Texto("Nenhum cliente neste roteiro. Clientes novos aparecem em Toda a carteira."));
        lista.Children.Add(InterfaceVenda.Texto("Pedidos confirmados pela distribuidora", true));
        foreach (var p in dados.Pedidos.OrderByDescending(x => x.RecebidoEm))
        {
            lista.Children.Add(InterfaceVenda.Texto($"{p.Cliente} · {p.Situacao}\n" + string.Join("\n",p.Itens.Select(i=>$"{i.Produto}: pediu {i.QuantidadePedida}, atendido {i.QuantidadeAtendida}"))));
            if(p.Situacao==SituacaoPedido.Recebido)lista.Children.Add(InterfaceVenda.Botao("Cancelar pedido recebido de "+p.Cliente,()=>CancelarPedido(p)));
        }
    }
    async Task CancelarPedido(PedidoDto pedido)
    {
        if(sessao.ExpiraEm<=DateTimeOffset.UtcNow)throw new InvalidOperationException("Entre novamente e atualize os pedidos antes de cancelar.");
        var motivo=await DisplayPromptAsync("Cancelar pedido","Informe o motivo. O cancelamento depende de conexão e de o pedido ainda não ter sido faturado.","Continuar","Voltar",maxLength:200);
        if(motivo is null)return;
        if(string.IsNullOrWhiteSpace(motivo))throw new InvalidOperationException("Informe o motivo do cancelamento.");
        using var api=new HttpClient{Timeout=TimeSpan.FromSeconds(20)};
        using var request=new HttpRequestMessage(HttpMethod.Post,new Uri(new Uri(sessao.ApiUrl),$"api/pedidos/{pedido.Id}/cancelar"));
        request.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",sessao.Token);
        request.Content=JsonContent.Create(new CancelamentoEntrada{Versao=pedido.Versao,Motivo=motivo.Trim()});
        using var response=await api.SendAsync(request);
        if(!response.IsSuccessStatusCode)
        {
            var texto=await response.Content.ReadAsStringAsync();
            string? mensagem=null;
            try{using var json=System.Text.Json.JsonDocument.Parse(texto);if(json.RootElement.TryGetProperty("mensagem",out var m))mensagem=m.GetString();}catch(System.Text.Json.JsonException){}
            throw new InvalidOperationException(mensagem??"Não foi possível cancelar. Atualize os pedidos e confira se o faturamento já ocorreu.");
        }
        var atualizado=(await response.Content.ReadFromJsonAsync<PedidoDto>())!;
        var indice=dados.Pedidos.FindIndex(p=>p.Id==atualizado.Id);
        if(indice>=0)dados.Pedidos[indice]=atualizado;
        await cache.Salvar(dados);await Atualizar();
        await DisplayAlertAsync("Pedido cancelado","A distribuidora confirmou o cancelamento.","OK");
    }
    Task AbrirCliente(ClienteDto cliente)
    {
        var divida = dados.Dividas.FirstOrDefault(x => x.ClienteId == cliente.Id);
        var corpo = new VerticalStackLayout { Padding = 20, Spacing = 12 };
        corpo.Children.Add(InterfaceVenda.Texto(cliente.Nome, true)); corpo.Children.Add(InterfaceVenda.Texto(cliente.Endereco));
        corpo.Children.Add(InterfaceVenda.Texto(divida is null ? "Cliente local: sem histórico financeiro baixado." : $"Dívida em {divida.DataConsulta:dd/MM/yyyy}: {InterfaceVenda.Dinheiro(divida.PrincipalAberto)}\nVencido: {InterfaceVenda.Dinheiro(divida.PrincipalVencido)}\nQuitação na data: {InterfaceVenda.Dinheiro(divida.ValorQuitacao)}"));
        corpo.Children.Add(InterfaceVenda.Botao("Montar pedido offline", () => Navigation.PushAsync(new PedidoOfflinePage(dados, cliente, trabalho))));
        corpo.Children.Add(InterfaceVenda.Texto("Última compra efetiva por produto", true));
        foreach (var compra in dados.UltimasCompras.Where(x => x.ClienteId == cliente.Id)) corpo.Children.Add(InterfaceVenda.Texto($"{compra.Produto}\n{compra.Data:dd/MM/yyyy}: {compra.Quantidade} · {InterfaceVenda.Dinheiro(compra.PrecoUnitario)}"));
        return Navigation.PushAsync(new ContentPage { Title = cliente.Nome, Content = new ScrollView { Content = corpo } });
    }
    Task Catalogo()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var corpo = new VerticalStackLayout { Padding = 20, Spacing = 12 };
        corpo.Children.Add(InterfaceVenda.Texto("Catálogo baixado", true));
        foreach (var p in dados.Produtos)
        {
            corpo.Children.Add(new Label { Text = $"{p.Nome} · {p.Unidade}\n{InterfaceVenda.Dinheiro(p.PrecoNaData(hoje))} {(p.EmPromocao(hoje) ? "PROMOÇÃO" : "")} {(p.Novidade(hoje) ? "NOVO" : "")}", TextColor = Colors.Black, Padding = 12,
                BackgroundColor = p.EmPromocao(hoje) ? Color.FromArgb("#FFF0A8") : p.Novidade(hoje) ? Color.FromArgb("#DFF0FF") : Color.FromArgb("#F2F3F5") });
        }
        return Navigation.PushAsync(new ContentPage { Title = "Produtos", Content = new ScrollView { Content = corpo } });
    }
}

public sealed class PendenciasPage : ContentPage
{
    readonly SessaoDispositivo sessao;
    readonly PreparacaoDto dados;
    readonly TrabalhoLocal local;
    readonly VerticalStackLayout lista = new() { Spacing = 12 };
    readonly Label mensagem = new();
    bool enviando;
    public PendenciasPage(SessaoDispositivo sessao, PreparacaoDto dados, TrabalhoLocal local)
    {
        this.sessao = sessao; this.dados = dados; this.local = local; Title = "Pedidos e envio";
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 20, Spacing = 14, Children = {
            InterfaceVenda.Texto("Envio das pendências", true), InterfaceVenda.Texto("Horários de referência: 12h e 19h. O envio só ocorre ao tocar no botão. Rascunhos não são enviados."),
            InterfaceVenda.Botao("Enviar pendências agora", Enviar), mensagem, lista
        }}};
    }
    protected override async void OnAppearing() { base.OnAppearing(); try { await Atualizar(); } catch (Exception ex) { mensagem.Text = ex.Message; } }
    async Task Atualizar()
    {
        lista.Children.Clear();
        foreach (var item in (await local.Listar(dados.VendedorId)).Where(x=>x.Situacao!="Descartado"))
        {
            var nome = item.Tipo == "Cliente" ? TrabalhoLocal.Ler<ClienteEntrada>(item).Nome : dados.Clientes.FirstOrDefault(c => c.Id == TrabalhoLocal.Ler<PedidoEntrada>(item).ClienteId)?.Nome ?? "Cliente local";
            var resumo = item.Tipo == "Pedido" ? "\n" + string.Join("\n", TrabalhoLocal.Ler<PedidoEntrada>(item).Itens.Select(i => $"{dados.Produtos.FirstOrDefault(p => p.Id == i.ProdutoId)?.Nome ?? i.ProdutoId.ToString()}: {i.Quantidade} × {InterfaceVenda.Dinheiro(i.PrecoUnitario)}")) : "";
            lista.Children.Add(InterfaceVenda.Texto($"{item.Tipo}: {nome} · {item.Situacao}{resumo}\n{item.Mensagem}"));
            if(item.Situacao is "Rascunho" or "Rejeitado" or "Pronto")
            {
                lista.Children.Add(InterfaceVenda.Botao("Revisar "+item.Tipo.ToLowerInvariant(),()=>Revisar(item)));
                lista.Children.Add(InterfaceVenda.Botao("Descartar "+item.Tipo.ToLowerInvariant(),()=>Descartar(item)));
            }
        }
    }
    async Task Descartar(OperacaoLocal item)
    {
        if(enviando)return;
        if(!await DisplayAlertAsync("Descartar pendência","Retirar esta pendência local da fila? Pedidos já confirmados na central devem ser cancelados na lista de pedidos recebidos.","Descartar","Voltar"))return;
        await local.Descartar(dados.VendedorId,item.Chave);await Atualizar();
    }
    async Task Revisar(OperacaoLocal item)
    {
        if (enviando) return;
        if (item.Tipo == "Cliente") { await Navigation.PushAsync(new ClienteOfflinePage(dados, local, TrabalhoLocal.Ler<ClienteEntrada>(item))); return; }
        var pedido = TrabalhoLocal.Ler<PedidoEntrada>(item);
        var clientes = TrabalhoLocal.Clientes(dados, await local.Listar(dados.VendedorId));
        var cliente = clientes.FirstOrDefault(x => x.Id == pedido.ClienteId) ?? throw new InvalidOperationException("Cliente fora da carteira baixada. Atualize os dados ou consulte a distribuidora.");
        await Navigation.PushAsync(new PedidoOfflinePage(dados, cliente, local, pedido));
    }
    async Task Enviar()
    {
        if (enviando) return;
        if (sessao.ExpiraEm <= DateTimeOffset.UtcNow) { mensagem.Text = "Volte à tela inicial e entre novamente para enviar. As pendências continuam salvas."; return; }
        if (!await DisplayAlertAsync("Confirmar envio", "Enviar os clientes e pedidos prontos para a distribuidora?", "Enviar", "Voltar")) return;
        enviando = true;
        try
        {
            using var api = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            mensagem.Text = await new EnvioPendencias(local, api).Enviar(dados.VendedorId, new Uri(sessao.ApiUrl), sessao.Token);
            await Atualizar();
        }
        finally { enviando = false; }
    }
}
