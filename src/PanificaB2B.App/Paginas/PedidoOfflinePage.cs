using System.Globalization;
using PanificaB2B.App.Dados;
using PanificaB2B.Contratos;

namespace PanificaB2B.App.Paginas;

public sealed class PedidoOfflinePage : ContentPage
{
    readonly PreparacaoDto dados;
    readonly ClienteDto cliente;
    readonly TrabalhoLocal trabalho;
    readonly PedidoEntrada entrada;
    readonly Picker condicao = new() { Title = "Condição de pagamento" };
    readonly Entry observacao = new() { Placeholder = "Observação", MaxLength = 300 };
    readonly Dictionary<Guid, Entry> quantidades = [];
    readonly Label erro = new() { TextColor = Color.FromArgb("#A84E37") };
    readonly Label totalResumo = TemaVenda.Titulo("R$ 0,00",28);
    readonly Label produtosResumo = TemaVenda.Legenda("Nenhum produto selecionado");
    public PedidoOfflinePage(PreparacaoDto dados, ClienteDto cliente, TrabalhoLocal trabalho, PedidoEntrada? pedido = null)
    {
        this.dados = dados; this.cliente = cliente; this.trabalho = trabalho;
        entrada = pedido ?? new() { ClienteId = cliente.Id, VendedorId = dados.VendedorId };
        Title = "Pedido · " + cliente.Nome;
        foreach (var c in dados.CondicoesPagamento) condicao.Items.Add(c.Nome + " · " + string.Join("/", c.Parcelas.Select(x => x.Dias)) + " dias");
        condicao.SelectedIndex = pedido is null ? (condicao.Items.Count > 0 ? 0 : -1) : dados.CondicoesPagamento.FindIndex(x => x.Id == pedido.CondicaoPagamentoId);
        observacao.Text = entrada.Observacao;
        var corpo=TemaVenda.Corpo();
        corpo.Children.Add(TemaVenda.Cabecalho("NOVO PEDIDO",cliente.Nome,"Monte o pedido durante a visita. Salvar não envia nada automaticamente."));
        corpo.Children.Add(TemaVenda.Cartao(TemaVenda.Campo("Condição de pagamento",condicao),TemaVenda.Campo("Observação para a distribuidora",observacao)));
        corpo.Children.Add(TemaVenda.Titulo("Escolha os produtos",20));
        var hoje=DateOnly.FromDateTime(DateTime.Today);
        var antigos=dados.UltimasCompras.Where(x=>x.ClienteId==cliente.Id).ToDictionary(x=>x.ProdutoId);
        if(entrada.Itens.Any(i=>dados.Produtos.All(p=>p.Id!=i.ProdutoId)))corpo.Children.Add(TemaVenda.Legenda("Um produto deste rascunho saiu do catálogo e será removido ao salvar. Confira os itens."));
        foreach(var produto in dados.Produtos.OrderByDescending(p=>antigos.ContainsKey(p.Id)).ThenBy(p=>p.Nome))
        {
            var quantidade=entrada.Itens.FirstOrDefault(i=>i.ProdutoId==produto.Id)?.Quantidade??0;
            var campo=new Entry{Placeholder="0",Keyboard=Keyboard.Numeric,MaxLength=16,Text=quantidade.ToString(CultureInfo.InvariantCulture),HorizontalTextAlignment=TextAlignment.Center,AutomationId="Quantidade_"+produto.Codigo};
            quantidades[produto.Id]=campo;campo.TextChanged+=(_,_)=>AtualizarResumo();
            var card=TemaVenda.Cartao(TemaVenda.Legenda(produto.Grupo+" · "+produto.Unidade),TemaVenda.Titulo(produto.Nome,18),TemaVenda.Titulo(InterfaceVenda.Dinheiro(produto.PrecoNaData(hoje)),22));
            var painel=(VerticalStackLayout)card.Content!;
            var etiquetas=new HorizontalStackLayout{Spacing=8};
            if(produto.EmPromocao(hoje))etiquetas.Children.Add(TemaVenda.Etiqueta("PROMOÇÃO",true));
            if(produto.Novidade(hoje))etiquetas.Children.Add(TemaVenda.Etiqueta("NOVO",azul:true));
            if(etiquetas.Children.Count>0)painel.Children.Insert(0,etiquetas);
            if(antigos.TryGetValue(produto.Id,out var compra))painel.Children.Add(TemaVenda.Legenda($"Última compra: {compra.Quantidade} em {compra.Data:dd/MM/yyyy} · {InterfaceVenda.Dinheiro(compra.PrecoUnitario)} por unidade"));
            void Mudar(decimal diferenca){campo.Text=Math.Clamp(InterfaceVenda.Numero(campo.Text)+diferenca,0,1000000).ToString(CultureInfo.InvariantCulture);}
            var menos=InterfaceVenda.Botao("−",()=>{Mudar(-1);return Task.CompletedTask;},true);menos.WidthRequest=48;
            var mais=InterfaceVenda.Botao("+",()=>{Mudar(1);return Task.CompletedTask;},true);mais.WidthRequest=48;
            var controles=new Grid{ColumnDefinitions={new ColumnDefinition(GridLength.Auto),new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)},ColumnSpacing=10};
            controles.Add(menos,0,0);controles.Add(campo,1,0);controles.Add(mais,2,0);
            painel.Children.Add(TemaVenda.Campo("Quantidade · "+produto.Unidade,controles));corpo.Children.Add(card);
        }
        corpo.Children.Add(TemaVenda.Cartao(TemaVenda.Legenda("PRÉVIA DO PEDIDO"),totalResumo,produtosResumo,TemaVenda.Legenda("A quantidade final e o faturamento serão confirmados pela distribuidora.")));
        corpo.Children.Add(erro);
        corpo.Children.Add(InterfaceVenda.Botao("Conferir pedido →",()=>Salvar(true)));
        corpo.Children.Add(InterfaceVenda.Botao("Salvar rascunho",()=>Salvar(false),true));
        Content=TemaVenda.Pagina(corpo);AtualizarResumo();
    }
    void AtualizarResumo()
    {
        try
        {
            var itens=dados.Produtos.Where(p=>quantidades.ContainsKey(p.Id)).Select(p=>new{Quantidade=InterfaceVenda.Numero(quantidades[p.Id].Text),Preco=p.PrecoNaData(DateOnly.FromDateTime(DateTime.Today))}).Where(i=>i.Quantidade>0).ToList();
            totalResumo.Text=InterfaceVenda.Dinheiro(itens.Sum(i=>decimal.Round(i.Quantidade*i.Preco,2,MidpointRounding.AwayFromZero)));
            produtosResumo.Text=$"{itens.Count} produto(s) selecionado(s)";totalResumo.TextColor=TemaVenda.Verde;
        }
        catch(Exception ex) when(ex is InvalidOperationException or OverflowException){totalResumo.Text="Confira as quantidades";totalResumo.TextColor=Color.FromArgb("#A84E37");produtosResumo.Text="Use valores positivos com até três casas decimais.";}
    }
    async Task Salvar(bool pronto)
    {
        try
        {
            if (condicao.SelectedIndex < 0) throw new InvalidOperationException("Baixe e selecione uma condição de pagamento ativa.");
            var cond = dados.CondicoesPagamento[condicao.SelectedIndex];
            entrada.CondicaoPagamentoId = cond.Id; entrada.CondicaoVersao = cond.Versao; entrada.Observacao = observacao.Text ?? "";
            entrada.Itens = dados.Produtos.Select(p => new ItemPedidoEntrada { ProdutoId = p.Id, ProdutoVersao = p.Versao, PrecoUnitario = p.PrecoNaData(DateOnly.FromDateTime(DateTime.Today)), Quantidade = InterfaceVenda.Numero(quantidades[p.Id].Text) }).Where(i => i.Quantidade > 0).ToList();
            if (pronto && entrada.Itens.Count == 0) throw new InvalidOperationException("Informe pelo menos uma quantidade.");
            if (pronto)
            {
                var total = entrada.Itens.Sum(i => decimal.Round(i.Quantidade * i.PrecoUnitario, 2, MidpointRounding.AwayFromZero));
                if (!await DisplayAlertAsync("Conferir pedido", $"{cliente.Nome}\n{entrada.Itens.Count} produtos · {InterfaceVenda.Dinheiro(total)}\n{cond.Nome}\nO pedido aguardará o botão de envio.", "Confirmar", "Voltar")) return;
            }
            await trabalho.SalvarPedido(dados.VendedorId, entrada, pronto);
            await DisplayAlertAsync("Salvo", pronto ? "Pedido pronto. Envie pela tela Pedidos e envio." : "Rascunho salvo sem enviar.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex) { erro.Text = ex.Message; }
    }
}
