using System.Globalization;
using System.Net.Http.Json;
using PanificaB2B.App.Dados;
using PanificaB2B.Contratos;

namespace PanificaB2B.App.Paginas;

public static class InterfaceVenda
{
    public static Label Texto(string texto, bool destaque = false) => new() { Text = texto, FontSize = destaque ? 20 : 14, TextColor = TemaVenda.Texto, FontFamily = destaque ? "OpenSansSemibold" : "OpenSansRegular", FontAttributes = destaque ? FontAttributes.Bold : FontAttributes.None };
    public static Button Botao(string texto, Func<Task> acao, bool secundario=false)
    {
        var button = new Button { Text = texto, BackgroundColor=secundario?TemaVenda.VerdeClaro:TemaVenda.Verde, TextColor=secundario?TemaVenda.Verde:Colors.White };
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
    readonly Picker roteiro = new() { Title = "Selecione o roteiro" };
    readonly SearchBar busca = new() { Placeholder = "Buscar cliente na carteira" };
    readonly Label resumo = TemaVenda.Legenda("");
    int versaoLista;
    PreparacaoDto dados;
    public OperacaoPage(SessaoDispositivo sessao, PreparacaoDto dados, CarteiraLocal cache, TrabalhoLocal trabalho)
    {
        this.sessao = sessao; this.dados = dados; this.cache = cache; this.trabalho = trabalho;
        Title = "Vendas do dia";
        roteiro.Items.Add("Toda a carteira");
        foreach (var r in dados.Roteiros) roteiro.Items.Add($"{InterfaceVenda.Dias[r.DiaSemana]} · {r.Nome}");
        roteiro.SelectedIndex = 0; roteiro.SelectedIndexChanged += async (_, _) => await Atualizar();
        busca.TextChanged+=async(_,_)=>await Atualizar();
        var corpo=TemaVenda.Corpo();
        corpo.Children.Add(TemaVenda.Cabecalho("MINHA ROTINA","Carteira e visitas",$"Vendedor: {sessao.Usuario.Login}"));
        corpo.Children.Add(TemaVenda.Cartao(TemaVenda.Etiqueta("CARTEIRA SALVA NO APARELHO"),TemaVenda.Legenda($"Preços e dívida consultados em {dados.AtualizadoEm.ToLocalTime():dd/MM/yyyy HH:mm}. Atualize os dados na tela inicial quando necessário.")));
        corpo.Children.Add(TemaVenda.Colunas(InterfaceVenda.Botao("Produtos",Catalogo,true),InterfaceVenda.Botao("Pedidos e envio",()=>Navigation.PushAsync(new PendenciasPage(sessao,dados,trabalho)),true)));
        corpo.Children.Add(InterfaceVenda.Botao("+ Cadastrar cliente",()=>Navigation.PushAsync(new ClienteOfflinePage(dados,trabalho))));
        corpo.Children.Add(TemaVenda.Campo("Roteiro de visitas",roteiro));
        corpo.Children.Add(busca);corpo.Children.Add(resumo);corpo.Children.Add(lista);
        Content=TemaVenda.Pagina(corpo);
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { dados = await cache.Carregar(dados.VendedorId) ?? dados; await Atualizar(); }
        catch (Exception ex) { await DisplayAlertAsync("Carteira", ex.Message, "OK"); }
    }
    async Task Atualizar()
    {
        var versao=++versaoLista;
        var operacoes=await trabalho.Listar(dados.VendedorId);
        if(versao!=versaoLista)return;
        lista.Children.Clear();
        var clientes=TrabalhoLocal.Clientes(dados,operacoes);
        var visitas=roteiro.SelectedIndex>0 && roteiro.SelectedIndex<=dados.Roteiros.Count?dados.Roteiros[roteiro.SelectedIndex-1].Visitas:null;
        if(visitas is not null)clientes=visitas.OrderBy(v=>v.Sequencia).Select(v=>clientes.FirstOrDefault(c=>c.Id==v.ClienteId)).OfType<ClienteDto>().ToList();
        var termo=LocalidadesBrasil.Normalizar(busca.Text);
        if(termo.Length>0)clientes=clientes.Where(c=>LocalidadesBrasil.Normalizar(c.Nome+" "+c.Endereco).Contains(termo)).ToList();
        resumo.Text=$"{clientes.Count} cliente(s) nesta seleção · {operacoes.Count(o=>o.Tipo=="Pedido" && o.Situacao is "Pronto" or "Pendente" or "Enviando")} pedido(s) aguardando envio";
        foreach(var cliente in clientes)
        {
            var sequencia=visitas?.FirstOrDefault(v=>v.ClienteId==cliente.Id)?.Sequencia;
            var divida=dados.Dividas.FirstOrDefault(d=>d.ClienteId==cliente.Id);
            var financeiro=divida is null?"Sem histórico financeiro baixado":$"Em aberto: {InterfaceVenda.Dinheiro(divida.PrincipalAberto)}";
            var cartao=TemaVenda.Cartao(TemaVenda.Etiqueta(sequencia is {} ordem?$"VISITA {ordem}":"CLIENTE DA CARTEIRA"),TemaVenda.Titulo(cliente.Nome,18),TemaVenda.Legenda(cliente.Endereco),InterfaceVenda.Texto(financeiro),InterfaceVenda.Botao("Abrir cliente →",()=>AbrirCliente(cliente),true));
            if(divida?.PrincipalVencido>0 && cartao.Content is VerticalStackLayout painel)painel.Children.Insert(3,TemaVenda.Etiqueta("Há parcela vencida",true));
            lista.Children.Add(cartao);
        }
        if(clientes.Count==0)lista.Children.Add(TemaVenda.Vazio("Nenhum cliente nesta seleção","Altere o roteiro ou a busca. Cadastros novos aparecem em Toda a carteira."));
        lista.Children.Add(TemaVenda.Titulo("Retorno da distribuidora",20));
        lista.Children.Add(TemaVenda.Legenda("Pedidos do último download, com o resultado do atendimento."));
        foreach(var p in dados.Pedidos.OrderByDescending(p=>p.RecebidoEm))
        {
            var status=p.Situacao switch{SituacaoPedido.Recebido=>"Aguardando análise",SituacaoPedido.Faturado=>"Faturado",SituacaoPedido.FaturadoParcialmente=>"Faturado com corte",SituacaoPedido.NaoAtendido=>"Não atendido",_=>"Cancelado"};
            var cartao=TemaVenda.Cartao(TemaVenda.Etiqueta(status,p.Situacao==SituacaoPedido.Recebido),TemaVenda.Titulo(p.Cliente,17),TemaVenda.Legenda(string.Join("\n",p.Itens.Select(i=>$"{i.Produto}: pediu {i.QuantidadePedida}, atendido {i.QuantidadeAtendida}"))));
            if(p.Situacao==SituacaoPedido.Recebido && cartao.Content is VerticalStackLayout painel)painel.Children.Add(InterfaceVenda.Botao("Cancelar pedido",()=>CancelarPedido(p),true));
            lista.Children.Add(cartao);
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
        var divida=dados.Dividas.FirstOrDefault(d=>d.ClienteId==cliente.Id);
        var corpo=TemaVenda.Corpo();
        corpo.Children.Add(TemaVenda.Cabecalho("FICHA DO CLIENTE",cliente.Nome,cliente.Endereco));
        if(!string.IsNullOrWhiteSpace(cliente.Telefone))corpo.Children.Add(TemaVenda.Legenda("Contato: "+cliente.Telefone));
        if(divida is null)corpo.Children.Add(TemaVenda.Vazio("Cadastro local","Sem histórico financeiro baixado para este cliente."));
        else
        {
            corpo.Children.Add(TemaVenda.Colunas(TemaVenda.Numero(InterfaceVenda.Dinheiro(divida.PrincipalAberto),"Principal em aberto"),TemaVenda.Numero(InterfaceVenda.Dinheiro(divida.PrincipalVencido),"Principal vencido")));
            corpo.Children.Add(TemaVenda.Legenda($"Consulta de {divida.DataConsulta:dd/MM/yyyy}. Valor para quitação na data: {InterfaceVenda.Dinheiro(divida.ValorQuitacao)}."));
        }
        corpo.Children.Add(InterfaceVenda.Botao("+ Montar pedido",()=>Navigation.PushAsync(new PedidoOfflinePage(dados,cliente,trabalho))));
        corpo.Children.Add(TemaVenda.Titulo("Última compra por produto",20));
        var compras=dados.UltimasCompras.Where(c=>c.ClienteId==cliente.Id).ToList();
        foreach(var compra in compras)corpo.Children.Add(TemaVenda.Cartao(TemaVenda.Titulo(compra.Produto,16),InterfaceVenda.Texto($"{compra.Quantidade} · {InterfaceVenda.Dinheiro(compra.PrecoUnitario)} por unidade"),TemaVenda.Legenda($"Compra efetiva em {compra.Data:dd/MM/yyyy}")));
        if(compras.Count==0)corpo.Children.Add(TemaVenda.Vazio("Ainda sem compras no histórico","Os produtos atendidos aparecerão aqui após o faturamento e a atualização da carteira."));
        return Navigation.PushAsync(new ContentPage{Title="Ficha do cliente",Content=TemaVenda.Pagina(corpo)});
    }
    Task Catalogo()
    {
        var hoje=DateOnly.FromDateTime(DateTime.Today);
        var corpo=TemaVenda.Corpo();
        corpo.Children.Add(TemaVenda.Cabecalho("PRODUTOS","Catálogo de panificação","Preços do último download. Promoções e novidades merecem atenção na visita."));
        var pesquisa=new SearchBar{Placeholder="Buscar produto ou grupo"};
        var produtos=new VerticalStackLayout{Spacing=12};
        void Renderizar()
        {
            produtos.Children.Clear();
            var termo=LocalidadesBrasil.Normalizar(pesquisa.Text);
            var filtrados=dados.Produtos.Where(p=>LocalidadesBrasil.Normalizar(p.Nome+" "+p.Grupo).Contains(termo)).OrderByDescending(p=>p.EmPromocao(hoje)).ThenByDescending(p=>p.Novidade(hoje)).ThenBy(p=>p.Nome).ToList();
            foreach(var p in filtrados)
            {
                var etiquetas=new HorizontalStackLayout{Spacing=8};
                if(p.EmPromocao(hoje))etiquetas.Children.Add(TemaVenda.Etiqueta("PROMOÇÃO",true));
                if(p.Novidade(hoje))etiquetas.Children.Add(TemaVenda.Etiqueta("NOVO",azul:true));
                var preco=TemaVenda.Titulo(InterfaceVenda.Dinheiro(p.PrecoNaData(hoje)),24);preco.TextColor=TemaVenda.Verde;
                var card=TemaVenda.Cartao(etiquetas,TemaVenda.Legenda(p.Grupo+" · "+p.Unidade),TemaVenda.Titulo(p.Nome,18),preco);
                if(p.EmPromocao(hoje) && card.Content is VerticalStackLayout painel)painel.Children.Add(TemaVenda.Legenda($"Preço normal: {InterfaceVenda.Dinheiro(p.Preco)} · oferta até {p.PromocaoFim:dd/MM/yyyy}"));
                produtos.Children.Add(card);
            }
            if(filtrados.Count==0)produtos.Children.Add(TemaVenda.Vazio("Nenhum produto encontrado","Tente outro nome ou atualize o catálogo na tela inicial."));
        }
        pesquisa.TextChanged+=(_,_)=>Renderizar();Renderizar();
        corpo.Children.Add(pesquisa);corpo.Children.Add(produtos);
        return Navigation.PushAsync(new ContentPage{Title="Produtos",Content=TemaVenda.Pagina(corpo)});
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
        var corpo=TemaVenda.Corpo();
        corpo.Children.Add(TemaVenda.Cabecalho("SINCRONIZAÇÃO","Pedidos e envio","Confira o que está salvo no aparelho e o que já foi confirmado pela central."));
        corpo.Children.Add(TemaVenda.Cartao(TemaVenda.Etiqueta("HORÁRIOS DE REFERÊNCIA · 12H / 19H"),TemaVenda.Legenda("O envio acontece somente ao confirmar o botão abaixo. Rascunhos ficam no aparelho.")));
        corpo.Children.Add(InterfaceVenda.Botao("Enviar pendências agora →",Enviar));
        mensagem.TextColor=TemaVenda.Verde;corpo.Children.Add(mensagem);corpo.Children.Add(lista);
        Content=TemaVenda.Pagina(corpo);
    }
    protected override async void OnAppearing() { base.OnAppearing(); try { await Atualizar(); } catch (Exception ex) { mensagem.Text = ex.Message; } }
    async Task Atualizar()
    {
        lista.Children.Clear();
        var itens=(await local.Listar(dados.VendedorId)).Where(x=>x.Situacao!="Descartado").ToList();
        var clientes=TrabalhoLocal.Clientes(dados,itens);
        foreach(var item in itens)
        {
            var nome=item.Tipo=="Cliente"?TrabalhoLocal.Ler<ClienteEntrada>(item).Nome:clientes.FirstOrDefault(c=>c.Id==TrabalhoLocal.Ler<PedidoEntrada>(item).ClienteId)?.Nome??"Cliente local";
            var resumo=item.Tipo=="Pedido"?string.Join("\n",TrabalhoLocal.Ler<PedidoEntrada>(item).Itens.Select(i=>$"{dados.Produtos.FirstOrDefault(p=>p.Id==i.ProdutoId)?.Nome??i.ProdutoId.ToString()}: {i.Quantidade} × {InterfaceVenda.Dinheiro(i.PrecoUnitario)}")):"Cadastro aguardando confirmação da distribuidora.";
            var card=TemaVenda.Cartao(TemaVenda.Etiqueta(item.Situacao,item.Situacao is "Pendente" or "Rejeitado" or "Conflito"),TemaVenda.Titulo(nome,18),TemaVenda.Legenda(item.Tipo),InterfaceVenda.Texto(resumo));
            var painel=(VerticalStackLayout)card.Content!;
            if(!string.IsNullOrWhiteSpace(item.Mensagem))painel.Children.Add(TemaVenda.Legenda(item.Mensagem));
            if(item.Situacao is "Rascunho" or "Rejeitado" or "Pronto")painel.Children.Add(TemaVenda.Colunas(InterfaceVenda.Botao("Revisar",()=>Revisar(item),true),InterfaceVenda.Botao("Descartar",()=>Descartar(item),true)));
            lista.Children.Add(card);
        }
        if(itens.Count==0)lista.Children.Add(TemaVenda.Vazio("Tudo organizado por aqui","Os cadastros e pedidos salvos neste aparelho aparecerão nesta lista."));
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
