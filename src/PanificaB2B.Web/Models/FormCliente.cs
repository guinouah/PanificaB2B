using PanificaB2B.Contratos;
namespace PanificaB2B.Web.Models;
public class FormCliente
{
    public ClienteEntrada Entrada { get; set; } = new();
    public List<CidadeDto> Cidades { get; set; } = [];
    public List<TerritorioDto> Territorios { get; set; } = [];
}
