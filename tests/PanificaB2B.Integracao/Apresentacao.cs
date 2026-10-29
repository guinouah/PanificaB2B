using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using PanificaB2B.App.Dados;
using PanificaB2B.Contratos;

namespace PanificaB2B.Integracao;

public static partial class Program
{
    static async Task VerificarApresentacao(string raiz,string pasta)
    {
        Directory.CreateDirectory(pasta);
        using var api=new FabricaApi(raiz,pasta);
        using var visitante=api.CreateClient();
        var senha=Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        await Requisicao(visitante,HttpMethod.Post,"/api/auth/configurar",new{login="admin.apresenta",senha});
        using var admin=await Entrar(api,"admin.apresenta",senha);
        Verificar(LocalidadesBrasil.Estados.Count==27 && LocalidadesBrasil.Estados.Sum(e=>e.Cidades.Count)>5500,"Catálogo oficial de UFs e municípios está disponível sem internet");
        await Requisicao(admin,HttpMethod.Post,"/api/formas-pagamento",new{nome="8"},HttpStatusCode.BadRequest);
        await Requisicao(admin,HttpMethod.Post,"/api/formas-pagamento",new{nome="Pagamento inventado"},HttpStatusCode.BadRequest);
        await Requisicao(admin,HttpMethod.Post,"/api/estados",new{nome="Inventado",sigla="ZZ"},HttpStatusCode.BadRequest);
        await Requisicao(admin,HttpMethod.Post,"/api/estados",new{nome="Paraná",sigla="SP"},HttpStatusCode.BadRequest);
        var sp=await Post<EstadoDto>(admin,"/api/estados",new{nome="São Paulo",sigla="SP"});
        await Requisicao(admin,HttpMethod.Post,"/api/cidades",new{nome="Foz do Iguaçu",estadoId=sp.Id},HttpStatusCode.BadRequest);
        var cidade=await Post<CidadeDto>(admin,"/api/cidades",new{nome="sao paulo",estadoId=sp.Id});
        await Requisicao(admin,HttpMethod.Post,"/api/cidades",new{nome="São Paulo",estadoId=sp.Id},HttpStatusCode.BadRequest);
        Verificar(cidade.Nome=="São Paulo","API recusa formas numéricas, UFs fictícias e municípios de outra UF; nomes oficiais são preservados");
        using var fabricaSite=new FabricaSite(raiz,pasta,api);
        using var site=fabricaSite.CreateClient();
        await Formulario(site,"/Conta/Entrar",new(){["Login"]="admin.apresenta",["Senha"]=senha});
        var paginas=await site.GetStringAsync("/Cadastros");
        var pagamentos=await site.GetStringAsync("/Pagamentos");
        Verificar(paginas.Contains("data-municipio-uf") && paginas.Contains("municipios-brasil") && pagamentos.Contains("id=\"nova-forma\"") && !pagamentos.Contains("placeholder=\"Nova forma\""),"Site apresenta seletores oficiais em cadastros e pagamentos");
        var formaInvalida=await Formulario(site,"/Pagamentos/Forma",new(){["Nome"]="8",["Ativo"]="true"},"/Pagamentos");
        Verificar(WebUtility.HtmlDecode(formaInvalida.Texto).Contains("Selecione uma forma de pagamento válida") && !(await admin.GetFromJsonAsync<List<FormaPagamentoDto>>("/api/formas-pagamento"))!.Any(f=>f.Nome=="8"),"Formulário do site também recusa forma numérica sem gravar cadastro");
        var demo=await Post<DemonstracaoDto>(admin,"/api/demonstracao/preparar",new{senhaAcessos=senha});
        Verificar(!demo.JaExistia,"Demonstração é preparada mediante solicitação do administrador");
        var pedidos=(await admin.GetFromJsonAsync<List<PedidoDto>>("/api/pedidos"))!.Where(p=>p.Situacao==SituacaoPedido.Recebido).OrderBy(p=>p.Itens.Single().QuantidadePedida).ToList();
        var produto=(await admin.GetFromJsonAsync<List<ProdutoDto>>("/api/produtos"))!.Single(p=>p.Codigo=="DEMO-FER-15");
        var lotes=(await admin.GetFromJsonAsync<List<LoteDto>>("/api/estoque/lotes"))!;
        Verificar(pedidos.Select(p=>p.Itens.Single().QuantidadePedida).SequenceEqual(new[]{5m,8m,9m}) && lotes.Where(l=>l.ProdutoId==produto.Id).Sum(l=>l.Saldo)==15,"Cenário pronto contém pedidos 5/8/9 e estoque de 15 unidades");
        var a=(await admin.GetFromJsonAsync<HistoricoPagamentosDto>($"/api/clientes/{pedidos[0].ClienteId}/historico-pagamentos"))!;
        var b=(await admin.GetFromJsonAsync<HistoricoPagamentosDto>($"/api/clientes/{pedidos[1].ClienteId}/historico-pagamentos"))!;
        var c=(await admin.GetFromJsonAsync<HistoricoPagamentosDto>($"/api/clientes/{pedidos[2].ClienteId}/historico-pagamentos"))!;
        Verificar(a.PagosNoPrazo==1 && a.PrincipalEmAberto==0 && b.PagosComAtraso==1 && b.MaiorAtrasoDias==4 && c.VencidosEmAberto==1 && c.PrincipalVencido==47,"Histórico distingue pagamento pontual, atraso pago e parcela ainda vencida");
        var tela=WebUtility.HtmlDecode(await site.GetStringAsync($"/Pedidos/Detalhe/{pedidos[2].Id}"));
        Verificar(tela.Contains("Pontualidade do cliente") && tela.Contains("Há parcelas vencidas") && tela.Contains("47,00"),"Tela do corte mostra dívida e pontualidade do cliente");
        await Requisicao(admin,HttpMethod.Post,"/api/produtos",new ProdutoEntrada{Codigo="INVALIDA",Nome="Teste",GrupoId=produto.GrupoId,Preco=10,Unidade="8"},HttpStatusCode.BadRequest);
        var antes=(await admin.GetFromJsonAsync<List<VendaDto>>("/api/vendas"))!.Count;
        await Requisicao(admin,HttpMethod.Post,$"/api/pedidos/{pedidos[0].Id}/faturar",new FaturamentoEntrada{Versao=pedidos[0].Versao,Motivo="Disponibilidade",Itens=[new(){ItemId=pedidos[0].Itens[0].Id,Quantidade=2}]});
        var repetida=await Post<DemonstracaoDto>(admin,"/api/demonstracao/preparar",new{senhaAcessos=senha});
        Verificar(repetida.JaExistia && (await admin.GetFromJsonAsync<List<VendaDto>>("/api/vendas"))!.Count==antes+1 && (await admin.GetFromJsonAsync<List<LoteDto>>("/api/estoque/lotes"))!.Where(l=>l.ProdutoId==produto.Id).Sum(l=>l.Saldo)==13,"Repetir demonstração não duplica registros nem restaura estoque já faturado");
        using var vendedor=await Entrar(api,"vendedor.demo",senha);
        using var financeiro=await Entrar(api,"financeiro.demo",senha);
        using var estoque=await Entrar(api,"estoque.demo",senha);
        await Requisicao(estoque,HttpMethod.Get,$"/api/clientes/{a.ClienteId}/historico-pagamentos",esperado:HttpStatusCode.Forbidden);
        await Requisicao(financeiro,HttpMethod.Get,$"/api/clientes/{a.ClienteId}/historico-pagamentos");
        await Requisicao(vendedor,HttpMethod.Get,$"/api/clientes/{a.ClienteId}/historico-pagamentos");
        await Requisicao(vendedor,HttpMethod.Post,"/api/demonstracao/preparar",new{senhaAcessos=senha},HttpStatusCode.Forbidden);
        await Requisicao(vendedor,HttpMethod.Post,$"/api/pedidos/{pedidos[1].Id}/cancelar",new CancelamentoEntrada{Versao=pedidos[1].Versao,Motivo="Cliente desistiu"});
        var faturado=(await vendedor.GetFromJsonAsync<PedidoDto>($"/api/pedidos/{pedidos[0].Id}"))!;
        await Requisicao(vendedor,HttpMethod.Post,$"/api/pedidos/{faturado.Id}/cancelar",new CancelamentoEntrada{Versao=faturado.Versao,Motivo="Tentativa depois de faturar"},HttpStatusCode.BadRequest);
        Verificar((await vendedor.GetFromJsonAsync<List<PedidoDto>>("/api/pedidos"))!.Single(p=>p.Id==pedidos[1].Id).Situacao==SituacaoPedido.Cancelado,"Vendedor cancela pedido recebido e não cancela pedido já faturado");
        var local=new TrabalhoLocal(Path.Combine(pasta,"descarte.db3"));
        var seller=Guid.NewGuid();var cliente=new ClienteEntrada{Id=Guid.NewGuid(),Nome="Cliente local",Endereco="Endereço",CidadeId=cidade.Id,TerritorioId=Guid.NewGuid()};
        var pedido=new PedidoEntrada{ClienteId=cliente.Id,VendedorId=seller};
        await local.SalvarCliente(seller,cliente);await local.SalvarPedido(seller,pedido,false);
        async Task Recusa(Func<Task> acao){try{await acao();throw new Exception("Descarte deveria ter sido recusado.");}catch(InvalidOperationException){}}
        await Recusa(()=>local.Descartar(Guid.NewGuid(),"Pedido:"+pedido.Id));
        await Recusa(()=>local.Descartar(seller,"Cliente:"+cliente.Id));
        await local.Descartar(seller,"Pedido:"+pedido.Id);await local.Descartar(seller,"Cliente:"+cliente.Id);
        Verificar((await local.Listar(seller)).All(p=>p.Situacao=="Descartado"),"Descarte preserva registros, exige titularidade e protege dependências de cliente");
        var incerto=new PedidoEntrada{ClienteId=cliente.Id,VendedorId=seller};
        await local.SalvarPedido(seller,incerto,true);
        var operacao=(await local.Listar(seller)).Single(p=>p.RegistroId==incerto.Id.ToString());
        await local.Atualizar(operacao,"Pendente","Resposta perdida");
        await Recusa(()=>local.Descartar(seller,operacao.Chave));
        Verificar((await local.Listar(seller)).Single(p=>p.Chave==operacao.Chave).Situacao=="Pendente","Resposta incerta da API não pode ser descartada como pedido nunca enviado");
    }
}
