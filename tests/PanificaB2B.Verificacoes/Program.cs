using PanificaB2B.App.Dados;
using PanificaB2B.Contratos;
using PanificaB2B.Api.Seguranca;

// Execute the actual app cache implementation, with no MAUI/Windows UI dependency.
var testRoot = Path.Combine(AppContext.BaseDirectory, "App_Data", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testRoot);
var path = Path.Combine(testRoot, "cache.db3");
var cache = new CarteiraLocal(path);
void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine($"OK: {name}"); }
Check(await cache.CarregarUltima() is null, "Banco novo inicia sem carteira");
var seller = Guid.NewGuid(); var territory = Guid.NewGuid(); var city = Guid.NewGuid(); var client = Guid.NewGuid();
var data = new PreparacaoDto(seller, DateTimeOffset.UtcNow, [], [], [],
    [new ClienteDto(client, "Cliente offline", "", "", "", "Endereço local", city, territory, true, 1)],
    [new RoteiroDto(Guid.NewGuid(), "Roteiro offline", seller, 2, true, 1, [new VisitaDto(client, "Cliente offline", 15)])]);
await cache.Salvar(data);
var loaded = await new CarteiraLocal(path).CarregarUltima();
Check(loaded?.Clientes.Single().Nome == "Cliente offline", "Cliente é recuperado do SQLite sem consultar a API");
Check(loaded?.Roteiros.Single().Visitas.Single().Sequencia == 15, "Roteiro e sequência são preservados");
var updated = data with { AtualizadoEm = data.AtualizadoEm.AddMinutes(1), Clientes = [] };
await cache.Salvar(updated);
Check((await cache.CarregarUltima())?.Clientes.Count == 0, "Atualização substitui a cópia antiga da carteira");
var other = data with { VendedorId = Guid.NewGuid(), AtualizadoEm = data.AtualizadoEm.AddMinutes(2) };
await cache.Salvar(other);
Check((await cache.CarregarUltima())?.VendedorId == other.VendedorId, "Última carteira corresponde ao download mais recente");
Check((await cache.Carregar(seller))?.VendedorId == seller, "Cache seleciona a carteira do vendedor informado");
Check(await cache.Carregar(Guid.NewGuid()) is null, "Cache não retorna carteira de outro vendedor por padrão");
Console.WriteLine("7 verificações do cache SQLite concluídas.");

var administrador = new IdentidadeUsuario(Guid.NewGuid(), PerfilUsuario.Administrador, null);
var vendedor = new IdentidadeUsuario(Guid.NewGuid(), PerfilUsuario.Vendedor, seller);
Check(AcessoUsuario.PodeAdministrar(administrador) && AcessoUsuario.PodeAcessarCarteira(administrador, seller), "Administrador tem acesso administrativo e à carteira");
Check(!AcessoUsuario.PodeAdministrar(vendedor), "Vendedor não administra a distribuidora");
Check(AcessoUsuario.PodeAcessarCarteira(vendedor, seller), "Vendedor acessa a própria carteira");
Check(!AcessoUsuario.PodeAcessarCarteira(vendedor, Guid.NewGuid()), "Vendedor não acessa outra carteira");
Check(!AcessoUsuario.PodeAcessarCarteira(vendedor with { VendedorId = null }, seller), "Vendedor sem vínculo não acessa carteira");
Check(!AcessoUsuario.PodeAcessarCarteira(vendedor with { Perfil = (PerfilUsuario)999 }, seller), "Perfil desconhecido não recebe acesso");
Check(!AcessoUsuario.PodeAcessarCarteira(administrador, Guid.Empty), "Carteira sem identificação não recebe acesso");
Check(!AcessoUsuario.PodeAdministrar(administrador with { UsuarioId = Guid.Empty }) && !AcessoUsuario.PodeAcessarCarteira(administrador with { UsuarioId = Guid.Empty }, seller), "Identidade sem usuário não recebe acesso");
Console.WriteLine("8 verificações das regras de acesso concluídas.");
