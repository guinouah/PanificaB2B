using Microsoft.EntityFrameworkCore;
using PanificaB2B.Api.Modelos;

namespace PanificaB2B.Api.Dados;

public static class DadosExemplo
{
    public static async Task Criar(DistribuidoraDbContext db)
    {
        if (await db.Estados.AnyAsync()) return;
        var estado = new Estado { Nome = "Paraná", Sigla = "PR" };
        var cidade = new Cidade { Nome = "Foz do Iguaçu", EstadoId = estado.Id };
        var vendedor = new Vendedor { Nome = "Henrique", Email = "henrique@example.test" };
        var outro = new Vendedor { Nome = "Ana", Email = "ana@example.test" };
        var territorio = new Territorio { Nome = "Foz · Centro / Porto Meira", VendedorId = vendedor.Id, Cidades = [new() { CidadeId = cidade.Id }] };
        var a = new Cliente { Nome = "Padaria Exemplo A", CidadeId = cidade.Id, TerritorioId = territorio.Id, Endereco = "Endereço fictício A" };
        var b = new Cliente { Nome = "Padaria Exemplo B", CidadeId = cidade.Id, TerritorioId = territorio.Id, Endereco = "Endereço fictício B" };
        var roteiro = new Roteiro { Nome = "Centro → Porto Meira", VendedorId = vendedor.Id, DiaSemana = 2, Visitas = [new() { ClienteId = a.Id, Sequencia = 10 }, new() { ClienteId = b.Id, Sequencia = 20 }] };
        db.AddRange(estado, cidade, vendedor, outro, territorio, a, b, roteiro);
        await db.SaveChangesAsync();
    }
}
