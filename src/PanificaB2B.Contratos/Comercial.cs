using System.ComponentModel.DataAnnotations;

namespace PanificaB2B.Contratos;

public enum SituacaoPedido { Recebido = 1, Faturado = 2, FaturadoParcialmente = 3, NaoAtendido = 4, Cancelado = 5 }
public sealed class ProdutoEntrada
{
    [Required, StringLength(30)] public string Codigo { get; set; } = "";
    [Required, StringLength(150)] public string Nome { get; set; } = "";
    public Guid GrupoId { get; set; }
    [Required, StringLength(15)] public string Unidade { get; set; } = "UN";
    [IntervaloDecimal("0.01", "1000000")] public decimal Preco { get; set; }
    [IntervaloDecimal("0.01", "1000000")] public decimal? PrecoPromocional { get; set; }
    public DateOnly? PromocaoInicio { get; set; }
    public DateOnly? PromocaoFim { get; set; }
    public DateOnly? NovidadeAte { get; set; }
    public bool Ativo { get; set; } = true;
    public int Versao { get; set; }
}
public record ProdutoDto(Guid Id, string Codigo, string Nome, Guid GrupoId, string Grupo, string Unidade, decimal Preco,
    decimal? PrecoPromocional, DateOnly? PromocaoInicio, DateOnly? PromocaoFim, DateOnly? NovidadeAte, bool Ativo, int Versao)
{
    public bool EmPromocao(DateOnly data) => PrecoPromocional.HasValue && PromocaoInicio <= data && PromocaoFim >= data;
    public decimal PrecoNaData(DateOnly data) => EmPromocao(data) ? PrecoPromocional!.Value : Preco;
    public bool Novidade(DateOnly data) => NovidadeAte >= data;
}
public sealed class FormaPagamentoEntrada
{
    [Required, StringLength(100)] public string Nome { get; set; } = "";
    public bool Ativo { get; set; } = true;
}
public record FormaPagamentoDto(Guid Id, string Nome, bool Ativo);
public sealed class ParcelaCondicaoEntrada
{
    [Range(0, 3650)] public int Dias { get; set; }
    [IntervaloDecimal("0.01", "100")] public decimal Percentual { get; set; }
    public Guid FormaPagamentoId { get; set; }
}
public sealed class CondicaoPagamentoEntrada
{
    [Required, StringLength(30)] public string Codigo { get; set; } = "";
    [Required, StringLength(100)] public string Nome { get; set; } = "";
    [IntervaloDecimal("0", "100")] public decimal MultaPercentual { get; set; }
    [IntervaloDecimal("0", "100")] public decimal JurosDiariosPercentual { get; set; }
    [IntervaloDecimal("0", "100")] public decimal DescontoDiarioPercentual { get; set; }
    public bool Ativo { get; set; } = true;
    public int Versao { get; set; }
    [Required, MinLength(1), MaxLength(24)] public List<ParcelaCondicaoEntrada> Parcelas { get; set; } = [];
}
public record ParcelaCondicaoDto(int Numero, int Dias, decimal Percentual, Guid FormaPagamentoId, string FormaPagamento);
public record CondicaoPagamentoDto(Guid Id, string Codigo, string Nome, bool Ativo, int Versao,
    decimal MultaPercentual, decimal JurosDiariosPercentual, decimal DescontoDiarioPercentual, List<ParcelaCondicaoDto> Parcelas);
public sealed class EntradaEstoque
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProdutoId { get; set; }
    [Required, StringLength(60)] public string CodigoLote { get; set; } = "";
    public DateOnly Validade { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
    [IntervaloDecimal("0.001", "1000000")] public decimal Quantidade { get; set; }
}
public sealed class PerdaEstoqueEntrada
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LoteId { get; set; }
    [IntervaloDecimal("0.001", "1000000")] public decimal Quantidade { get; set; }
    [Required, StringLength(200)] public string Motivo { get; set; } = "";
    public int Versao { get; set; }
}
public record LoteDto(Guid Id, Guid ProdutoId, string Produto, string Unidade, string Codigo, DateOnly Validade, decimal Saldo, int Versao);
public record MovimentoDto(Guid Id, Guid ProdutoId, string Produto, string Lote, decimal Quantidade, string Tipo, string Motivo, DateTimeOffset CriadoEm);
public sealed class ItemPedidoEntrada
{
    public Guid ProdutoId { get; set; }
    [IntervaloDecimal("0.001", "1000000")] public decimal Quantidade { get; set; }
    [IntervaloDecimal("0.01", "1000000")] public decimal PrecoUnitario { get; set; }
    public int ProdutoVersao { get; set; }
}
public sealed class PedidoEntrada
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClienteId { get; set; }
    public Guid VendedorId { get; set; }
    public Guid CondicaoPagamentoId { get; set; }
    public int CondicaoVersao { get; set; }
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
    [StringLength(300)] public string? Observacao { get; set; } = "";
    [Required, MinLength(1), MaxLength(200)] public List<ItemPedidoEntrada> Itens { get; set; } = [];
}
public record ItemPedidoDto(Guid Id, Guid ProdutoId, string Produto, string Unidade, decimal QuantidadePedida, decimal QuantidadeAtendida, decimal PrecoUnitario);
public record PedidoDto(Guid Id, Guid ClienteId, string Cliente, Guid VendedorId, string Vendedor, Guid CondicaoPagamentoId,
    string CondicaoPagamento, DateTimeOffset CriadoEm, DateTimeOffset RecebidoEm, SituacaoPedido Situacao, int Versao, string Observacao,
    decimal TotalPedido, List<ItemPedidoDto> Itens, Guid? VendaId);
public sealed class CorteItemEntrada
{
    public Guid ItemId { get; set; }
    [IntervaloDecimal("0", "1000000")] public decimal Quantidade { get; set; }
}
public sealed class FaturamentoEntrada
{
    public int Versao { get; set; }
    [StringLength(200)] public string? Motivo { get; set; } = "";
    [Required, MinLength(1)] public List<CorteItemEntrada> Itens { get; set; } = [];
}
public sealed class CancelamentoEntrada
{
    public int Versao { get; set; }
    [Required, StringLength(200)] public string Motivo { get; set; } = "";
}
public sealed class ConfirmacaoEntrada { public int Versao { get; set; } }
public record ItemVendaDto(Guid ProdutoId, string Produto, string Unidade, decimal Quantidade, decimal PrecoUnitario, decimal Total);
public record VendaDto(Guid Id, int Numero, Guid PedidoId, Guid ClienteId, string Cliente, string Vendedor, string CondicaoPagamento,
    DateOnly Data, decimal Total, bool Cancelada, bool Entregue, int Versao, List<ItemVendaDto> Itens);
public record FaturamentoDto(PedidoDto Pedido, VendaDto? Venda);
public record TituloDto(Guid Id, Guid VendaId, Guid ClienteId, string Cliente, string Identificacao, DateOnly Vencimento,
    decimal ValorOriginal, decimal SaldoPrincipal, decimal Multa, decimal Juros, decimal Desconto, decimal ValorQuitacao,
    Guid FormaPagamentoId, string FormaPagamento, bool Pago, bool Cancelado, int Versao);
public sealed class RecebimentoEntrada
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Versao { get; set; }
    public DateOnly Data { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public Guid FormaPagamentoId { get; set; }
    [IntervaloDecimal("0.01", "1000000000")] public decimal Valor { get; set; }
}
public record RecebimentoDto(Guid Id, Guid TituloId, DateOnly Data, decimal Valor, decimal Principal, decimal Multa, decimal Juros, decimal Desconto, string FormaPagamento);
public record UltimaCompraDto(Guid ClienteId, Guid ProdutoId, string Produto, DateOnly Data, decimal Quantidade, decimal PrecoUnitario);
public record DividaClienteDto(Guid ClienteId, DateOnly DataConsulta, decimal PrincipalAberto, decimal PrincipalVencido, decimal ValorQuitacao, List<TituloDto> Titulos);
public record PainelDto(int ClientesAtivos, int ProdutosAtivos, int PedidosPendentes, decimal Vendas, decimal ContasAReceber, int LotesVencidos);
