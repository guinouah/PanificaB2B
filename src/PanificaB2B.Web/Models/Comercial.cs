using System.ComponentModel.DataAnnotations;
using PanificaB2B.Contratos;

namespace PanificaB2B.Web.Models;

public sealed class FormProduto
{
    public ProdutoEntrada Entrada { get; set; } = new();
    public List<GrupoProdutoDto> Grupos { get; set; } = [];
}
public sealed class FormCondicao
{
    public CondicaoPagamentoEntrada Entrada { get; set; } = new();
    public List<FormaPagamentoDto> Formas { get; set; } = [];
}
public sealed class PagamentosModel
{
    public List<FormaPagamentoDto> Formas { get; set; } = [];
    public List<CondicaoPagamentoDto> Condicoes { get; set; } = [];
}
public sealed class FormEntradaEstoque
{
    public EntradaEstoque Entrada { get; set; } = new();
    public List<ProdutoDto> Produtos { get; set; } = [];
}
public sealed class EstoqueModel
{
    public List<LoteDto> Lotes { get; set; } = [];
    public List<MovimentoDto> Movimentos { get; set; } = [];
}
public sealed class FormPerda
{
    public PerdaEstoqueEntrada Entrada { get; set; } = new();
    public LoteDto? Lote { get; set; }
}
public sealed class LinhaPedido
{
    public Guid ProdutoId { get; set; }
    public int ProdutoVersao { get; set; }
    public decimal PrecoUnitario { get; set; }
    [IntervaloDecimal("0", "1000000")] public decimal Quantidade { get; set; }
}
public sealed class FormPedido
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
    public Guid ClienteId { get; set; }
    public Guid CondicaoPagamentoId { get; set; }
    public int CondicaoVersao { get; set; }
    [StringLength(300)] public string? Observacao { get; set; } = "";
    public List<LinhaPedido> Itens { get; set; } = [];
    public List<ClienteDto> Clientes { get; set; } = [];
    public List<ProdutoDto> Produtos { get; set; } = [];
    public List<CondicaoPagamentoDto> Condicoes { get; set; } = [];
}
public sealed class FormFaturamento
{
    public HistoricoPagamentosDto? Historico { get; set; }
    public PedidoDto Pedido { get; set; } = null!;
    public FaturamentoEntrada Corte { get; set; } = new();
    public List<LoteDto> Lotes { get; set; } = [];
}
public sealed class PedidosModel
{
    public List<PedidoDto> Pedidos { get; set; } = [];
    public List<LoteDto> Lotes { get; set; } = [];
}
public sealed class FinanceiroModel
{
    public DateOnly Data { get; set; }
    public List<TituloDto> Titulos { get; set; } = [];
    public List<RecebimentoDto> Recebimentos { get; set; } = [];
}
public sealed class FormReceber
{
    public RecebimentoEntrada Entrada { get; set; } = new();
    public TituloDto Titulo { get; set; } = null!;
    public List<FormaPagamentoDto> Formas { get; set; } = [];
}
public sealed class CadastrosModel
{
    public List<EstadoDto> Estados { get; set; } = [];
    public List<CidadeDto> Cidades { get; set; } = [];
    public List<VendedorDto> Vendedores { get; set; } = [];
    public List<TerritorioDto> Territorios { get; set; } = [];
}
public sealed class FormRoteiro
{
    public RoteiroEntrada Entrada { get; set; } = new();
    public List<VendedorDto> Vendedores { get; set; } = [];
    public List<ClienteDto> Clientes { get; set; } = [];
    public List<TerritorioDto> Territorios { get; set; } = [];
}
public sealed record ClienteDetalhes(ClienteDto Cliente, DividaClienteDto Divida, List<UltimaCompraDto> Compras);
