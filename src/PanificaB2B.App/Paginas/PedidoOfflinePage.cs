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
    readonly Label erro = new() { TextColor = Colors.IndianRed };
    public PedidoOfflinePage(PreparacaoDto dados, ClienteDto cliente, TrabalhoLocal trabalho, PedidoEntrada? pedido = null)
    {
        this.dados = dados; this.cliente = cliente; this.trabalho = trabalho;
        entrada = pedido ?? new() { ClienteId = cliente.Id, VendedorId = dados.VendedorId };
        Title = "Pedido · " + cliente.Nome;
        foreach (var c in dados.CondicoesPagamento) condicao.Items.Add(c.Nome + " · " + string.Join("/", c.Parcelas.Select(x => x.Dias)) + " dias");
        condicao.SelectedIndex = pedido is null ? (condicao.Items.Count > 0 ? 0 : -1) : dados.CondicoesPagamento.FindIndex(x => x.Id == pedido.CondicaoPagamentoId);
        observacao.Text = entrada.Observacao;
        var corpo = new VerticalStackLayout { Padding = 20, Spacing = 12, Children = { InterfaceVenda.Texto(cliente.Nome, true), InterfaceVenda.Texto("Salvo neste dispositivo. A distribuidora só receberá após confirmar o envio."), condicao, observacao } };
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var antigos = dados.UltimasCompras.Where(x => x.ClienteId == cliente.Id).ToDictionary(x => x.ProdutoId);
        if (entrada.Itens.Any(i => dados.Produtos.All(p => p.Id != i.ProdutoId))) corpo.Children.Add(InterfaceVenda.Texto("Um produto deste rascunho saiu do catálogo e será removido ao salvar. Confira os itens."));
        foreach (var produto in dados.Produtos.OrderByDescending(p => antigos.ContainsKey(p.Id)).ThenBy(p => p.Nome))
        {
            var quantidade = entrada.Itens.FirstOrDefault(x => x.ProdutoId == produto.Id)?.Quantidade ?? 0;
            var campo = new Entry { Placeholder = "Quantidade", Keyboard = Keyboard.Numeric, Text = quantidade.ToString(CultureInfo.InvariantCulture), TextColor = Colors.Black, BackgroundColor = Colors.White, PlaceholderColor = Colors.DimGray };
            quantidades[produto.Id] = campo;
            var ultimo = antigos.TryGetValue(produto.Id, out var compra) ? $"\nÚltima: {compra.Quantidade} em {compra.Data:dd/MM/yyyy} a {InterfaceVenda.Dinheiro(compra.PrecoUnitario)}" : "";
            var descricao = InterfaceVenda.Texto($"{produto.Nome} · {produto.Unidade}\nPreço atual: {InterfaceVenda.Dinheiro(produto.PrecoNaData(hoje))} {(produto.EmPromocao(hoje) ? "PROMOÇÃO" : "")} {(produto.Novidade(hoje) ? "NOVO" : "")}{ultimo}");
            if (produto.EmPromocao(hoje) || produto.Novidade(hoje)) descricao.TextColor = Colors.Black;
            var linha = new VerticalStackLayout { Padding = 12, Children = { descricao, campo },
                BackgroundColor = produto.EmPromocao(hoje) ? Color.FromArgb("#FFF0A8") : produto.Novidade(hoje) ? Color.FromArgb("#DFF0FF") : Colors.Transparent };
            corpo.Children.Add(linha);
        }
        corpo.Children.Add(erro);
        corpo.Children.Add(InterfaceVenda.Botao("Salvar rascunho", () => Salvar(false)));
        corpo.Children.Add(InterfaceVenda.Botao("Conferir e deixar pronto para envio", () => Salvar(true)));
        Content = new ScrollView { Content = corpo };
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
