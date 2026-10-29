namespace PanificaB2B.Api.Modelos;

public abstract class Cadastro
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nome { get; set; } = "";
    public bool Ativo { get; set; } = true;
}
public class Estado : Cadastro { public string Sigla { get; set; } = ""; }
public class Cidade : Cadastro { public Guid EstadoId { get; set; } }
public class Vendedor : Cadastro { public string Email { get; set; } = ""; }
public class Territorio : Cadastro
{
    public Guid VendedorId { get; set; }
    public int Versao { get; set; } = 1;
    public List<TerritorioCidade> Cidades { get; set; } = [];
}
public class TerritorioCidade { public Guid TerritorioId { get; set; } public Guid CidadeId { get; set; } }
public class Cliente : Cadastro
{
    public string Documento { get; set; } = "";
    public string Email { get; set; } = "";
    public string Telefone { get; set; } = "";
    public string Endereco { get; set; } = "";
    public Guid CidadeId { get; set; }
    public Guid TerritorioId { get; set; }
    public int Versao { get; set; } = 1;
}
public class Roteiro : Cadastro
{
    public Guid VendedorId { get; set; }
    public int DiaSemana { get; set; }
    public int Versao { get; set; } = 1;
    public List<ItemRoteiro> Visitas { get; set; } = [];
}
public class ItemRoteiro
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoteiroId { get; set; }
    public Guid ClienteId { get; set; }
    public int Sequencia { get; set; }
}
