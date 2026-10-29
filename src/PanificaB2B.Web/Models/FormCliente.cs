using PanificaB2B.Contratos;
namespace PanificaB2B.Web.Models;
public class FormCliente
{
    public ClienteEntrada Entrada { get; set; } = new();
    public List<EstadoDto> Estados { get; set; } = [];
    public List<CidadeDto> Cidades { get; set; } = [];
    public List<TerritorioDto> Territorios { get; set; } = [];
}
