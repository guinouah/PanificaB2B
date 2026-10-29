using System.ComponentModel.DataAnnotations;

namespace PanificaB2B.Contratos;

public sealed class EmailOpcionalAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null || value is string text && (string.IsNullOrWhiteSpace(text) || new EmailAddressAttribute().IsValid(text));
}

public record EstadoDto(Guid Id, string Nome, string Sigla, bool Ativo);
public record CidadeDto(Guid Id, string Nome, Guid EstadoId, bool Ativo);
public record VendedorDto(Guid Id, string Nome, string Email, bool Ativo);
public record TerritorioDto(Guid Id, string Nome, Guid VendedorId, List<Guid> CidadeIds, bool Ativo, int Versao);
public record ClienteDto(Guid Id, string Nome, string Documento, string Email, string Telefone, string Endereco, Guid CidadeId, Guid TerritorioId, bool Ativo, int Versao);
public record VisitaDto(Guid ClienteId, string NomeCliente, int Sequencia);
public record RoteiroDto(Guid Id, string Nome, Guid VendedorId, int DiaSemana, bool Ativo, int Versao, List<VisitaDto> Visitas);

public class EstadoEntrada
{
    [Required, StringLength(100)] public string Nome { get; set; } = "";
    [Required, StringLength(2, MinimumLength = 2)] public string Sigla { get; set; } = "";
    public bool Ativo { get; set; } = true;
}
public class CidadeEntrada
{
    [Required, StringLength(100)] public string Nome { get; set; } = "";
    public Guid EstadoId { get; set; }
    public bool Ativo { get; set; } = true;
}
public class VendedorEntrada
{
    [Required, StringLength(150)] public string Nome { get; set; } = "";
    [EmailOpcional, StringLength(150)] public string? Email { get; set; }
    public bool Ativo { get; set; } = true;
}
public class TerritorioEntrada
{
    [Required, StringLength(150)] public string Nome { get; set; } = "";
    public Guid VendedorId { get; set; }
    [Required, MinLength(1)] public List<Guid> CidadeIds { get; set; } = [];
    public bool Ativo { get; set; } = true;
    public int Versao { get; set; }
}
public class ClienteEntrada
{
    // Stable Id supplied by the app makes an offline registration identifiable on retry.
    public Guid Id { get; set; }
    [Required, StringLength(150)] public string Nome { get; set; } = "";
    [StringLength(30)] public string? Documento { get; set; }
    [EmailOpcional, StringLength(150)] public string? Email { get; set; }
    [StringLength(40)] public string? Telefone { get; set; }
    [Required, StringLength(250)] public string Endereco { get; set; } = "";
    public Guid CidadeId { get; set; }
    public Guid TerritorioId { get; set; }
    public bool Ativo { get; set; } = true;
    public int Versao { get; set; }
}
public class VisitaEntrada
{
    public Guid ClienteId { get; set; }
    [Range(1, int.MaxValue)] public int Sequencia { get; set; }
}
public class RoteiroEntrada
{
    [Required, StringLength(150)] public string Nome { get; set; } = "";
    public Guid VendedorId { get; set; }
    [Range(0, 6)] public int DiaSemana { get; set; }
    public bool Ativo { get; set; } = true;
    public int Versao { get; set; }
    [Required] public List<VisitaEntrada> Visitas { get; set; } = [];
}
public record PreparacaoDto(Guid VendedorId, DateTimeOffset AtualizadoEm, List<EstadoDto> Estados, List<CidadeDto> Cidades, List<TerritorioDto> Territorios, List<ClienteDto> Clientes, List<RoteiroDto> Roteiros)
{
    public List<GrupoProdutoDto> Grupos { get; init; } = [];
    public List<ProdutoDto> Produtos { get; init; } = [];
    public List<FormaPagamentoDto> FormasPagamento { get; init; } = [];
    public List<CondicaoPagamentoDto> CondicoesPagamento { get; init; } = [];
    public List<UltimaCompraDto> UltimasCompras { get; init; } = [];
    public List<DividaClienteDto> Dividas { get; init; } = [];
    public List<PedidoDto> Pedidos { get; init; } = [];
}
