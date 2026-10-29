using Microsoft.EntityFrameworkCore;
using PanificaB2B.Api.Dados;
using PanificaB2B.Api.Modelos;
using PanificaB2B.Api.Seguranca;
using PanificaB2B.Contratos;

namespace PanificaB2B.Api.Servicos;

public sealed class DemonstracaoService(DistribuidoraDbContext db,SenhasService senhas)
{
    static Guid Id(int n)=>Guid.Parse($"d3a00000-0000-4000-8000-{n:000000000000}");
    public async Task<DemonstracaoDto> Preparar(Guid administrador,string senha)
    {
        await using var tx=await db.Database.BeginTransactionAsync();
        if(await db.Produtos.AnyAsync(p=>p.Id==Id(20)))
            return new(true,"A demonstração já foi carregada. Os atendimentos e dados existentes foram preservados.");
        if(senha.Length<12)throw new RegraNegocioException("Defina uma senha com pelo menos 12 caracteres para os acessos de demonstração.");
        var logins=new[]{"vendedor.demo","gestor.demo","financeiro.demo","estoque.demo"};
        if(await db.Usuarios.AnyAsync(u=>logins.Contains(u.Login)))throw new RegraNegocioException("Já existe um acesso com nome reservado para demonstração. Nenhum dado foi substituído.");
        var hoje=Valores.Hoje;
        var estado=await db.Estados.SingleOrDefaultAsync(e=>e.Sigla=="PR");
        if(estado is null){estado=new(){Id=Id(1),Nome="Paraná",Sigla="PR"};db.Estados.Add(estado);}
        if(!estado.Ativo)throw new RegraNegocioException("Ative o estado Paraná antes de preparar a demonstração.");
        var cidade=await db.Cidades.SingleOrDefaultAsync(c=>c.EstadoId==estado.Id && c.Nome=="Foz do Iguaçu");
        if(cidade is null){cidade=new(){Id=Id(2),Nome="Foz do Iguaçu",EstadoId=estado.Id};db.Cidades.Add(cidade);}
        if(!cidade.Ativo)throw new RegraNegocioException("Ative Foz do Iguaçu antes de preparar a demonstração.");
        var vendedor=new Vendedor{Id=Id(3),Nome="[DEMO] Vendedor apresentação",Email="vendedor@example.test"};
        var territorio=new Territorio{Id=Id(4),Nome="[DEMO] Centro de Foz",VendedorId=vendedor.Id,Cidades=[new(){TerritorioId=Id(4),CidadeId=cidade.Id}]};
        var nomes=new[]{"[DEMO] Padaria Aurora","[DEMO] Confeitaria Doce Trigo","[DEMO] Pão da Vila"};
        var clientes=nomes.Select((nome,i)=>new Cliente{Id=Id(5+i),Nome=nome,Endereco="Endereço fictício de demonstração",CidadeId=cidade.Id,TerritorioId=territorio.Id}).ToList();
        db.AddRange(vendedor,territorio);db.Clientes.AddRange(clientes);
        db.Roteiros.Add(new(){Id=Id(8),Nome="[DEMO] Roteiro da apresentação",VendedorId=vendedor.Id,DiaSemana=(int)DateTime.Today.DayOfWeek,Visitas=clientes.Select((c,i)=>new ItemRoteiro{Id=Id(9+i),RoteiroId=Id(8),ClienteId=c.Id,Sequencia=(i+1)*10}).ToList()});
        var grupo=new GrupoProduto{Id=Id(15),Nome="[DEMO] Panificação",NomeNormalizado="[DEMO] PANIFICAÇÃO"};
        db.GruposProduto.Add(grupo);
        var fermento=new Produto{Id=Id(20),Codigo="DEMO-FER-15",Nome="[DEMO] Fermento biológico",GrupoId=grupo.Id,PrecoCentavos=1000,Unidade="UN",NovidadeAte=hoje.AddDays(30)};
        var mistura=new Produto{Id=Id(21),Codigo="DEMO-MISTURA",Nome="[DEMO] Mistura para bolo",GrupoId=grupo.Id,PrecoCentavos=4700,Unidade="KG"};
        var margarina=new Produto{Id=Id(22),Codigo="DEMO-MARG20",Nome="[DEMO] Margarina 20 L",GrupoId=grupo.Id,PrecoCentavos=15000,Unidade="BALDE",PromocaoCentavos=12000,PromocaoInicio=hoje,PromocaoFim=hoje.AddDays(7)};
        db.AddRange(fermento,mistura,margarina);
        var pix=await db.FormasPagamento.SingleOrDefaultAsync(f=>f.NomeNormalizado=="PIX");
        if(pix is null){pix=new(){Id=Id(25),Nome="Pix",NomeNormalizado="PIX"};db.FormasPagamento.Add(pix);}
        if(!pix.Ativo)throw new RegraNegocioException("Ative Pix antes de preparar a demonstração.");
        var condicao=new CondicaoPagamento{Id=Id(26),Codigo="DEMO7-14",Nome="[DEMO] 7 e 14 dias",MultaPontos=500,JurosDiariosPontos=200,DescontoDiarioPontos=100,
            Parcelas=[new(){Id=Id(27),CondicaoId=Id(26),Numero=1,Dias=7,PercentualPontos=5000,FormaPagamentoId=pix.Id},new(){Id=Id(28),CondicaoId=Id(26),Numero=2,Dias=14,PercentualPontos=5000,FormaPagamentoId=pix.Id}]};
        var historica=new CondicaoPagamento{Id=Id(29),Codigo="DEMO7",Nome="[DEMO] Histórico em 7 dias",MultaPontos=500,JurosDiariosPontos=200,
            Parcelas=[new(){Id=Id(30),CondicaoId=Id(29),Numero=1,Dias=7,PercentualPontos=10000,FormaPagamentoId=pix.Id}]};
        db.AddRange(condicao,historica);
        var saldos=new[]{15000L,47000L,20000L};
        var produtos=new[]{fermento,mistura,margarina};
        for(var i=0;i<produtos.Length;i++)
        {
            var lote=new LoteEstoque{Id=Id(40+i),ProdutoId=produtos[i].Id,Codigo="DEMO-APRESENTACAO",Validade=hoje.AddDays(i==0?5:30),SaldoMilesimos=saldos[i]};
            db.Lotes.Add(lote);
            db.Movimentos.Add(new(){Id=Id(45+i),LoteId=lote.Id,ProdutoId=lote.ProdutoId,QuantidadeMilesimos=saldos[i]+(i==1?3000:0),Tipo="Entrada",Motivo="Dados fictícios para apresentação",UsuarioId=administrador});
        }
        var quantidades=new[]{5,8,9};
        var numero=await db.Vendas.MaxAsync(v=>(int?)v.Numero)??0;
        for(var i=0;i<clientes.Count;i++)
        {
            var cliente=clientes[i];
            var pendente=new Pedido{Id=Id(60+i),ClienteId=cliente.Id,ClienteNome=cliente.Nome,VendedorId=vendedor.Id,VendedorNome=vendedor.Nome,CondicaoPagamentoId=condicao.Id,CondicaoNome=condicao.Nome,CondicaoVersao=1,CriadoEm=DateTimeOffset.UtcNow,ConteudoHash="DEMO-PENDENTE-"+i,Observacao="Exemplo do professor: pedidos 5/8/9, estoque 15; definir atendimento manual.",
                Itens=[new(){Id=Id(65+i),PedidoId=Id(60+i),ProdutoId=fermento.Id,ProdutoNome=fermento.Nome,Unidade="UN",QuantidadeMilesimos=quantidades[i]*1000,PrecoCentavos=1000,ProdutoVersao=1}]};
            var data=hoje.AddDays(-30);
            var pedidoAntigo=new Pedido{Id=Id(70+i),ClienteId=cliente.Id,ClienteNome=cliente.Nome,VendedorId=vendedor.Id,VendedorNome=vendedor.Nome,CondicaoPagamentoId=historica.Id,CondicaoNome=historica.Nome,CondicaoVersao=1,CriadoEm=new DateTimeOffset(data.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero),RecebidoEm=new DateTimeOffset(data.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero),Situacao=SituacaoPedido.Faturado,ConteudoHash="DEMO-HISTORICO-"+i,FaturamentoHash="DEMO-ATENDIDO-"+i,ResponsavelId=administrador,EncerradoEm=DateTimeOffset.UtcNow,
                Itens=[new(){Id=Id(75+i),PedidoId=Id(70+i),ProdutoId=mistura.Id,ProdutoNome=mistura.Nome,Unidade="KG",QuantidadeMilesimos=1000,AtendidaMilesimos=1000,PrecoCentavos=4700,ProdutoVersao=1}]};
            var venda=new Venda{Id=Id(80+i),Numero=++numero,PedidoId=pedidoAntigo.Id,ClienteId=cliente.Id,ClienteNome=cliente.Nome,VendedorId=vendedor.Id,VendedorNome=vendedor.Nome,CondicaoNome=historica.Nome,Data=data,TotalCentavos=4700,UsuarioId=administrador,Entregue=true,EntregueEm=DateTimeOffset.UtcNow,
                Itens=[new(){Id=Id(85+i),VendaId=Id(80+i),ProdutoId=mistura.Id,ProdutoNome=mistura.Nome,Unidade="KG",QuantidadeMilesimos=1000,PrecoCentavos=4700,TotalCentavos=4700}]};
            var vencimento=data.AddDays(7);
            var titulo=new TituloReceber{Id=Id(90+i),VendaId=venda.Id,ClienteId=cliente.Id,ClienteNome=cliente.Nome,Identificacao=$"{venda.Numero}/1",Parcela=1,Vencimento=vencimento,ValorCentavos=4700,FormaPagamentoId=pix.Id,FormaNome=pix.Nome,MultaPontos=500,JurosDiariosPontos=200,Pago=i<2};
            db.AddRange(pendente,pedidoAntigo,venda,titulo);
            db.Movimentos.Add(new(){Id=Id(95+i),LoteId=Id(41),ProdutoId=mistura.Id,QuantidadeMilesimos=-1000,Tipo="Venda",Motivo="Histórico fictício de demonstração",UsuarioId=administrador,VendaId=venda.Id});
            if(i<2)db.Recebimentos.Add(new(){Id=Id(100+i),TituloId=titulo.Id,Data=vencimento.AddDays(i==0?0:4),ValorCentavos=i==0?4700:5311,PrincipalCentavos=4700,MultaCentavos=i==0?0:235,JurosCentavos=i==0?0:376,FormaPagamentoId=pix.Id,FormaNome=pix.Nome,UsuarioId=administrador});
        }
        var perfis=new[]{PerfilUsuario.Vendedor,PerfilUsuario.GestorComercial,PerfilUsuario.Financeiro,PerfilUsuario.EstoqueExpedicao};
        for(var i=0;i<logins.Length;i++)
        {
            var usuario=new Usuario{Id=Id(110+i),Login=logins[i],LoginNormalizado=logins[i].ToUpperInvariant(),Perfil=perfis[i],VendedorId=i==0?vendedor.Id:null};
            usuario.SenhaHash=senhas.Gerar(usuario,senha);db.Usuarios.Add(usuario);
        }
        await db.SaveChangesAsync();await tx.CommitAsync();
        return new(false,"Demonstração preparada: três pedidos de 5/8/9, estoque de 15, condições, acessos e histórico de pagamentos.");
    }
}
