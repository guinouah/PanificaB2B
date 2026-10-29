# Escopo da entrega acadêmica

## Componentes

| Componente | Tecnologia | Responsabilidade |
|---|---|---|
| Aplicativo do vendedor | C#, .NET 10, .NET MAUI, sqlite-net-pcl | Carteira, roteiro, catálogo, compras anteriores, dívida, clientes e pedidos offline, envio confirmado |
| Site da distribuidora | ASP.NET Core MVC | Cadastros, roteiros, demanda, estoque, corte, faturamento, entrega e financeiro |
| API | ASP.NET Core Web API, EF Core | Autenticação, cinco perfis, autorização por carteira, regras e transações |
| Banco central | SQLite | Fonte oficial da distribuidora, acessada somente pela API |
| Banco do dispositivo | SQLite separado | Cache por vendedor e trabalho pendente; sem compartilhamento de arquivo |
| Assistência de IA | Codex, OpenAI | Apoio à análise, implementação, revisão e testes |

Os padrões específicos da aula que não constam da transcrição fornecida não foram presumidos como exigências.

## Cobertura do pedido do professor

| Requisito | Entrega |
|---|---|
| Aplicativo, site e API em C# | Três projetos e contratos compartilhados na solução |
| Carteira por vendedor e território | API valida responsável e vínculos; não basta trocar o id na URL |
| Roteiros por dia, várias cidades e áreas da mesma cidade | Território contém cidades e roteiro contém visitas ordenadas |
| Sequências 10, 20, 30 e inserção 15 | Manutenção de roteiro com sequência única |
| Cliente novo pelo vendedor e pela distribuidora | Site e cadastro offline no aplicativo, com id estável |
| Download de carteira, preços e dados para trabalhar | Preparação do vendedor e cache SQLite |
| Última compra efetiva, quantidade e preço | Derivada de itens de vendas válidas; não depende da semana anterior |
| Dívida do cliente | Principal, vencido, quitação e data de consulta |
| Catálogo com grupos, novidades e promoções | Produtos com preços, períodos e destaque por cor |
| Pedido salvo offline e enviado depois | Fila independente do cache, estados e botão de envio |
| Envio às 12h/19h e nova tentativa | Referências de horário, confirmação manual e idempotência por registro |
| Todos os pedidos na distribuidora | Site apresenta pedidos e demanda consolidada |
| Estoque insuficiente e corte manual | Quantidade solicitada preservada e quantidade atendida escolhida pelo gestor |
| Cobrar só o atendimento | Venda e títulos calculados sobre as quantidades liberadas |
| Impedir saldo negativo | Transação e validação dos lotes; concorrência testada |
| Perecibilidade | Lote, validade, saída pelo vencimento mais próximo, perdas e bloqueio de vencidos |
| Venda, itens e nota | Venda com itens históricos e demonstrativo fiscal simulado |
| Formas e condições de pagamento | Cadastros separados, forma por parcela e percentuais de 100% |
| Contas a receber e duplicatas | Geração automática, prazos desde a venda, ajuste de centavos e identificação número/parcela |
| Multa, juros, descontos e recebimento | Taxas configuráveis, cálculo datado e baixa integral com discriminação |
| Cadastros ativos/inativos | Preservam histórico e restringem novas operações |
| Sem limite de crédito inicial | Dívida informa a operação, sem bloqueio automático por crédito |
| Especificação da aplicação e tecnologias | Este documento, README e documentos de estrutura |

## Dados e consistência

1. O pedido recebe um GUID antes de sair do dispositivo. O mesmo GUID e conteúdo confirmam o mesmo registro; conteúdo diferente gera conflito.
2. Novos clientes são enviados antes dos pedidos dependentes. A confirmação é individual.
3. O servidor determina o acesso pela sessão autenticada, sem confiar no perfil informado pela tela.
4. O preço enviado é conferido com a versão do produto e a promoção vigente. Divergência exige revisão.
5. O atendimento não supera a quantidade pedida nem o saldo de lotes válidos.
6. Faturamento, baixa, títulos e situação do pedido pertencem à mesma transação.
7. No máximo uma venda por pedido. Uma segunda confirmação não gera outra cobrança.
8. Valores usam centavos e quantidades usam milésimos no banco. Totais são calculados no servidor.
9. Cadastros não são excluídos para corrigir operações; inativação e estorno preservam rastros.
10. O cache oficial e as pendências locais usam tabelas diferentes. Um download não apaga trabalho offline.
11. As regras financeiras são copiadas para os títulos. Mudar o cadastro não altera as parcelas emitidas.
12. A atualização da base anterior é aditiva e precedida por backup consistente.

## Limite da conclusão

Esta entrega conclui o fluxo funcional acadêmico local. Faturamento é simulado, o envio é manual e o recebimento é integral por parcela. Pagamentos parciais, devoluções após entrega, estorno de recebimentos, recuperação de senha, integração fiscal/bancária e hospedagem pública não foram exigidos como operações completas na versão combinada e não são apresentados como implementados.

As compilações e os testes executados estão registrados em VALIDACAO.md. Compilar ou usar TestServer não equivale a executar a interface em um aparelho; a demonstração manual deve seguir GUIA-DE-TESTE.md.
