using PanificaB2B.Contratos;

namespace PanificaB2B.Api.Modelos;

public class Produto : Cadastro
{
    public string Codigo { get; set; } = "";
    public Guid GrupoId { get; set; }
    public string Unidade { get; set; } = "UN";
    public long PrecoCentavos { get; set; }
    public long? PromocaoCentavos { get; set; }
    public DateOnly? PromocaoInicio { get; set; }
    public DateOnly? PromocaoFim { get; set; }
    public DateOnly? NovidadeAte { get; set; }
    public int Versao { get; set; } = 1;
}
public class FormaPagamento : Cadastro { public string NomeNormalizado { get; set; } = ""; }
public class CondicaoPagamento : Cadastro
{
    public string Codigo { get; set; } = "";
    public int MultaPontos { get; set; }
    public int JurosDiariosPontos { get; set; }
    public int DescontoDiarioPontos { get; set; }
    public int Versao { get; set; } = 1;
    public List<ParcelaCondicao> Parcelas { get; set; } = [];
}
public class ParcelaCondicao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CondicaoId { get; set; }
    public int Numero { get; set; }
    public int Dias { get; set; }
    public int PercentualPontos { get; set; }
    public Guid FormaPagamentoId { get; set; }
}
public class LoteEstoque
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProdutoId { get; set; }
    public string Codigo { get; set; } = "";
    public DateOnly Validade { get; set; }
    public long SaldoMilesimos { get; set; }
    public int Versao { get; set; } = 1;
}
public class MovimentoEstoque
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LoteId { get; set; }
    public Guid ProdutoId { get; set; }
    public long QuantidadeMilesimos { get; set; }
    public string Tipo { get; set; } = "";
    public string Motivo { get; set; } = "";
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
    public Guid UsuarioId { get; set; }
    public Guid? VendaId { get; set; }
}
public class Pedido
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string ClienteNome { get; set; } = "";
    public Guid VendedorId { get; set; }
    public string VendedorNome { get; set; } = "";
    public Guid CondicaoPagamentoId { get; set; }
    public string CondicaoNome { get; set; } = "";
    public int CondicaoVersao { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset RecebidoEm { get; set; } = DateTimeOffset.UtcNow;
    public SituacaoPedido Situacao { get; set; } = SituacaoPedido.Recebido;
    public int Versao { get; set; } = 1;
    public string Observacao { get; set; } = "";
    public string ConteudoHash { get; set; } = "";
    public string FaturamentoHash { get; set; } = "";
    public string MotivoEncerramento { get; set; } = "";
    public Guid? ResponsavelId { get; set; }
    public DateTimeOffset? EncerradoEm { get; set; }
    public List<ItemPedido> Itens { get; set; } = [];
}
public class ItemPedido
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PedidoId { get; set; }
    public Guid ProdutoId { get; set; }
    public string ProdutoNome { get; set; } = "";
    public string Unidade { get; set; } = "";
    public long QuantidadeMilesimos { get; set; }
    public long AtendidaMilesimos { get; set; }
    public long PrecoCentavos { get; set; }
    public int ProdutoVersao { get; set; }
}
public class Venda
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Numero { get; set; }
    public Guid PedidoId { get; set; }
    public Guid ClienteId { get; set; }
    public string ClienteNome { get; set; } = "";
    public Guid VendedorId { get; set; }
    public string VendedorNome { get; set; } = "";
    public string CondicaoNome { get; set; } = "";
    public DateOnly Data { get; set; }
    public long TotalCentavos { get; set; }
    public Guid UsuarioId { get; set; }
    public bool Cancelada { get; set; }
    public string MotivoCancelamento { get; set; } = "";
    public bool Entregue { get; set; }
    public DateTimeOffset? EntregueEm { get; set; }
    public int Versao { get; set; } = 1;
    public List<ItemVenda> Itens { get; set; } = [];
}
public class ItemVenda
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VendaId { get; set; }
    public Guid ProdutoId { get; set; }
    public string ProdutoNome { get; set; } = "";
    public string Unidade { get; set; } = "";
    public long QuantidadeMilesimos { get; set; }
    public long PrecoCentavos { get; set; }
    public long TotalCentavos { get; set; }
}
public class TituloReceber
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VendaId { get; set; }
    public Guid ClienteId { get; set; }
    public string ClienteNome { get; set; } = "";
    public string Identificacao { get; set; } = "";
    public int Parcela { get; set; }
    public DateOnly Vencimento { get; set; }
    public long ValorCentavos { get; set; }
    public Guid FormaPagamentoId { get; set; }
    public string FormaNome { get; set; } = "";
    public int MultaPontos { get; set; }
    public int JurosDiariosPontos { get; set; }
    public int DescontoDiarioPontos { get; set; }
    public bool Pago { get; set; }
    public bool Cancelado { get; set; }
    public int Versao { get; set; } = 1;
}
public class Recebimento
{
    public Guid Id { get; set; }
    public Guid TituloId { get; set; }
    public DateOnly Data { get; set; }
    public long ValorCentavos { get; set; }
    public long PrincipalCentavos { get; set; }
    public long MultaCentavos { get; set; }
    public long JurosCentavos { get; set; }
    public long DescontoCentavos { get; set; }
    public Guid FormaPagamentoId { get; set; }
    public string FormaNome { get; set; } = "";
    public Guid UsuarioId { get; set; }
}
