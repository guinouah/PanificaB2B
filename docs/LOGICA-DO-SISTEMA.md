# Lógica do sistema — distribuidora B2B

Revisão baseada no texto completo do professor fornecido pelo usuário. A lógica abaixo orienta a solução acadêmica local implementada em PanificaB2B. Propostas de evolução que ultrapassam essa versão continuam identificadas; README, ENTREGA.md e VALIDACAO.md descrevem a cobertura real.

**Legenda:** requisito = informado pelo professor; proposta = decisão de modelagem para viabilizar o requisito; pendência = detalhe que a fala não resolve.

## 1. Os três componentes e seus dados

| Componente | Responsabilidade | Dados |
|---|---|---|
| Aplicativo do vendedor | Baixar carteira, roteiro e preços; consultar histórico e dívida; cadastrar cliente; montar pedidos e enviar pendências | SQLite local, incluindo dados baixados e operações ainda não enviadas |
| API | Autenticar, autorizar, sincronizar, validar regras e executar transações | Acesso ao banco central |
| Site da distribuidora | Manter cadastros e roteiros; receber pedidos; controlar estoque; cortar, faturar e acompanhar recebimentos | Consulta e altera o banco central por meio da API |

SQLite foi solicitado pelo usuário. A proposta é utilizá-lo no aplicativo e, para o projeto acadêmico, em um arquivo central separado, acessado exclusivamente pela API.

**Os arquivos SQLite não são compartilhados nem copiados entre os dispositivos.** O aplicativo guarda uma cópia parcial e sincroniza registros pela API. O site não abre o banco do vendedor.

Dados oficiais de estoque, faturamento e financeiro pertencem à distribuidora. O aplicativo mantém os dados necessários para trabalhar sem conexão, mostrando quando foram atualizados.

## 2. Carteira, território e roteiro

São conceitos relacionados, com responsabilidades diferentes:

- **Território:** área de atuação definida pela empresa. Pode conter várias cidades ou uma parte de uma cidade.
- **Carteira:** conjunto de clientes atribuídos ao vendedor pelos territórios sob sua responsabilidade.
- **Roteiro:** lista ordenada dos clientes que o vendedor visita em determinado dia da semana.

Exemplos do professor:

| Vendedor | Dia | Abrangência do roteiro |
|---|---|---|
| Henrique | Segunda | Santa Terezinha e São Miguel |
| Henrique | Terça | Foz: Centro em direção ao Porto Meira |
| Henrique | Quarta | Foz: Centro em direção à Vila C |

Os dias acima apenas ilustram a distribuição. A empresa cadastra os dias efetivos.

Cada item do roteiro relaciona um cliente à sua sequência: 10, 20, 30. Um novo cliente entre 10 e 20 pode receber 15.

**Correção do esboço anterior:** a sequência fica no item do roteiro, pois pertence à visita daquele roteiro. Não fica como uma única sequência permanente no cliente.

Propostas de integridade:

1. Um território possui um responsável comercial vigente por vez.
2. Um cliente pertence a um território; seu vendedor atual é obtido por essa atribuição.
3. Um roteiro pertence a um vendedor e a um dia da semana; o vendedor pode ter vários roteiros.
4. A mesma cidade pode participar de territórios distintos, desde que a empresa delimite as áreas sem conflito.
5. Um roteiro pode conter clientes de mais de uma cidade.
6. Um cliente não se repete no mesmo roteiro e duas visitas do mesmo roteiro não usam a mesma sequência.
7. O cliente deve pertencer à carteira do vendedor responsável pelo roteiro.
8. Transferir a carteira não altera o vendedor registrado nos pedidos e vendas históricos.

O sistema valida os vínculos. A delimitação de duas áreas dentro da mesma cidade é uma decisão da empresa; cadastrar a mesma cidade não identifica, sozinho, uma sobreposição territorial.

## 3. Preparação do aplicativo

Com conexão, o vendedor usa a opção de baixar/atualizar:

- Sua carteira e seus roteiros.
- Cidades e estados necessários ao cadastro.
- Produtos comercializáveis e grupos.
- Lista de preços, promoções e produtos novos.
- Condições e formas de pagamento.
- Últimas compras de cada cliente.
- Títulos em aberto e valores devidos, com data de atualização.
- Situação dos pedidos já enviados e atendimento realizado.

O saldo exato do estoque **não é enviado ao vendedor**, conforme o professor. Essa restrição também deve existir na resposta da API, não apenas na tela.

O download atualiza cópias recebidas da distribuidora. Ele não pode apagar pedidos locais nem cadastros de clientes ainda não enviados.

Nesta versão, cada preparação é um retrato completo da carteira autorizada e substitui somente o cache; as pendências ficam em outra tabela. Atualização incremental paginada por marcador de versão permanece uma proposta de evolução e precisaria comunicar inativações e remoções de vínculos explicitamente.

## 4. Trabalho sem conexão

Durante a visita:

1. O aplicativo lista os clientes do roteiro, em ordem.
2. Ao selecionar um cliente, carrega suas compras anteriores.
3. Mostra, por produto, a quantidade e o preço da última compra efetiva.
4. Mostra os valores devidos conforme a última atualização.
5. Permite consultar todo o catálogo comercializável, com destaque para novidades e promoções.
6. O vendedor monta e salva o pedido no SQLite local.
7. Ao concluir, confirma que o pedido está pronto para envio.

A última compra é buscada no histórico de vendas, sem presumir que aconteceu na semana anterior. Um feriado não apaga essa referência.

**Pedido montado localmente não significa pedido recebido pela distribuidora.** A interface deve deixar essa diferença visível.

Um cliente novo pode ser cadastrado no aplicativo, com seus dados empresariais, contato e e-mail. A API deve receber esse cadastro antes de aceitar os pedidos que dependem dele.

## 5. Estados de negócio e de sincronização

Separar as duas dimensões evita confundir falha de internet com rejeição comercial.

### Estado de negócio do pedido

| Estado | Significado |
|---|---|
| Rascunho | Em edição no aplicativo |
| Pronto para envio | Conferido pelo vendedor |
| Recebido | Aceito e persistido pela API |
| Em análise | Distribuidora está definindo o atendimento |
| Faturado integralmente | Todos os itens foram atendidos |
| Faturado parcialmente | Parte das quantidades foi cortada |
| Não atendido | Quantidade atendida igual a zero em todos os itens |
| Cancelado | Cancelamento permitido antes do faturamento |

Esses nomes são uma proposta; o comportamento é a parte essencial.

### Situação local de sincronização

| Situação | Tratamento |
|---|---|
| Pendente | Aguarda tentativa de envio |
| Enviando | Há uma tentativa em andamento |
| Confirmado pela API | Servidor confirmou a gravação |
| Falha de conexão | Mantém a pendência para tentar novamente |
| Rejeitado para correção | Mostra a validação da API e exige intervenção |

Rascunhos não entram no envio. Depois de recebido, o pedido não deve ser sobrescrito silenciosamente pelo aplicativo. Correção ou cancelamento precisa de operação explícita.

## 6. Envio às 12h e às 19h, com repetição segura

Requisito: concentrar o envio nos horários indicados e oferecer um botão para enviar pedidos pendentes, permitindo nova tentativa.

Proposta para a primeira versão: horários de referência às 12h e às 19h, com confirmação pelo botão. Não bloquear o envio fora dessas janelas. Execução automática em segundo plano não está assumida: precisa ser definida separadamente, considerando as limitações do dispositivo.

O envio segue esta ordem:

1. Enviar os novos clientes ainda pendentes.
2. Receber a confirmação individual de cada cadastro.
3. Enviar os pedidos prontos cujos clientes já foram aceitos.
4. Receber uma resposta individual para cada pedido.
5. Marcar como confirmado apenas o que a API efetivamente aceitou.

Se cinco pedidos forem enviados e três forem confirmados, apenas os dois restantes continuam pendentes.

### Como impedir duplicatas

Proposta: cliente novo e pedido recebem um identificador estável, gerado no dispositivo, antes do primeiro envio. A tentativa reutiliza o mesmo identificador.

Exemplo:

- O aplicativo envia o pedido P123.
- A API grava P123, mas a conexão cai antes da resposta.
- O aplicativo envia P123 novamente.
- A API encontra o pedido já registrado e retorna a confirmação existente.

Não criar outro pedido porque a resposta se perdeu.

O servidor valida o identificador junto com o vendedor autenticado e o conteúdo. O mesmo identificador com conteúdo diferente gera conflito, em vez de substituir o registro.

Pedidos de vendedores diferentes podem ser recebidos no mesmo período. Uma transação por pedido permite confirmar alguns e informar o erro dos demais sem deixar itens incompletos.

### Conflitos na atualização

A distribuidora controla território, carteira, roteiro, preços, status e regras financeiras. O aplicativo não pode sobrescrever essas informações com cópias antigas.

Se um cliente for transferido, um produto inativado ou um preço alterado enquanto o vendedor estiver sem conexão, a API devolve o conflito para conferência. Não redirecionar silenciosamente um pedido nem recalcular seu preço sem informar o vendedor.

Para cadastros duplicados feitos em dispositivos diferentes, a identificação empresarial normalizada deve permitir detectar o cliente existente. Uma associação a outra carteira exige análise da distribuidora.

## 7. Preços, promoções e produtos novos

O aplicativo mostra o catálogo, o preço atualizado e a referência da última compra do cliente.

O item do pedido guarda o preço que foi aplicado durante a montagem e a versão da lista utilizada. O item da venda guarda o preço confirmado no faturamento. Alterações posteriores no cadastro não reescrevem o histórico.

Proposta: o preço da última compra é informativo. Não reaplicá-lo automaticamente, pois pode ter sido promocional.

Promoção e novidade são informações distintas:

- Promoção: produto, preço promocional e período de vigência.
- Novidade: indicador de lançamento com um período de destaque proposto.
- Um produto pode ter ambos os destaques.
- O catálogo baixado deve conter informações suficientes para o destaque por cor.

Validação pendente: preço válido no momento da coleta do pedido ou no momento em que a API o recebe? A estrutura preserva o preço coletado e permite resolver o conflito sem perder a informação original.

## 8. Estoque perecível e corte

A distribuidora vê a demanda de todos os vendedores. O estoque limita o atendimento final, e não a digitação do pedido no aplicativo.

Exemplo do professor:

| Cliente | Pedido | Atendimento | Corte |
|---|---:|---:|---:|
| A | 5 | 2 | 3 |
| B | 8 | 6 | 2 |
| C | 9 | 7 | 2 |
| **Total** | **22** | **15** | **7** |

Com 15 unidades disponíveis, o gestor escolhe a distribuição de 2, 6 e 7. O sistema não impõe um rateio automático. O histórico de pagamentos pode apoiar a decisão.

Regras:

- Atendimento entre zero e a quantidade pedida.
- Soma atendida não pode superar o estoque disponível na confirmação.
- Preservar a quantidade pedida e a efetivamente atendida.
- Cobrar apenas o que será atendido.
- Não gerar venda nem cobrança para um pedido totalmente cortado.
- Registrar responsável e data do corte; motivo é uma proposta de rastreabilidade.
- Não transformar automaticamente a quantidade cortada em outro pedido.

### Lotes e validade — proposta de modelagem

Como os produtos são perecíveis, propõe-se controlar entradas por lote e validade. O professor exige perecibilidade e estoque; não detalha o cadastro de lotes.

Um produto pode ter vários lotes. A saída prioriza o vencimento mais próximo entre os lotes válidos. Lotes vencidos ficam registrados, mas não são disponibilizados para venda.

Entradas, saídas, perdas e ajustes geram movimentos rastreáveis. Uma simulação de corte não baixa estoque. A confirmação deve revalidar o saldo dentro da transação, inclusive quando dois operadores estiverem trabalhando.

## 9. Pedido, faturamento e entrega

O professor descreve: receber o pedido, analisar/cortar, transformar em venda, faturar e organizar a entrega.

Proposta inicial: a confirmação do faturamento executa uma única transação local no banco central:

1. Verificar o estado atual do pedido.
2. Validar todos os itens e o estoque.
3. Registrar as quantidades atendidas e os cortes.
4. Criar a venda e seus itens com os valores confirmados.
5. Registrar a saída dos lotes e os movimentos de estoque.
6. Gerar as parcelas de contas a receber.
7. Atualizar o resultado do pedido.
8. Confirmar todas as alterações.

Se uma etapa falhar, desfazer todas as gravações da operação.

Uma confirmação repetida não gera nova venda nem novas parcelas. Proposta inicial: no máximo uma venda por pedido. Entregas complementares ou devoluções exigem regras próprias.

A entrega física é um evento posterior ao faturamento. Se houver diferença entre faturado e realmente entregue, o sistema precisa de um ajuste explícito; não pode manter cobrança por quantidade não recebida.

O usuário definiu que a nota fiscal será simulada. Não haverá integração fiscal real nesta entrega acadêmica. A identificação simulada permite relacionar a venda às duplicatas sem depender de serviços externos.

## 10. Condição e forma de pagamento

**Condição de pagamento** define prazos, percentuais das parcelas, multa, juros e desconto por antecipação.

**Forma de pagamento** define o meio de quitação: Pix, boleto ou outro cadastrado.

Proposta de estrutura:

- Condição de pagamento: código, descrição, ativo e regras financeiras.
- Parcela da condição: número, prazo em dias desde a venda, percentual e forma de pagamento prevista.
- Forma de pagamento: identificação, descrição e ativo.

Uma condição pode ter primeira parcela por Pix e segunda por boleto. Por isso, a forma prevista precisa estar associada a cada parcela, não somente à venda.

Ao faturar, copiar a configuração utilizada para a venda e seus títulos. Alterar a condição cadastrada depois não altera parcelas já emitidas.

### Exemplos do professor

Venda de R$ 235,00, realizada em 19/10:

| Parcela | Percentual | Prazo desde a venda | Vencimento | Valor |
|---|---:|---:|---|---:|
| 1 | 50% | 7 dias | 26/10 | R$ 117,50 |
| 2 | 50% | 14 dias | 02/11 | R$ 117,50 |

Os prazos partem da data da venda, não do vencimento da parcela anterior.

Outra condição: 33,33%, 33,33% e 33,34%, com prazos de 7, 14 e 21 dias. Para R$ 235,00, o arredondamento monetário resulta em R$ 78,33, R$ 78,33 e R$ 78,34.

Regras de geração:

1. Percentuais somam exatamente 100%.
2. Valores são calculados com precisão decimal e arredondados para centavos.
3. A última parcela recebe o ajuste de centavos necessário para fechar o total.
4. A soma dos títulos deve ser exatamente igual ao valor da venda.
5. Cada título tem parcela, valor original, vencimento e forma prevista.
6. Uma condição à vista pode usar prazo zero e desconto configurado.
7. Rejeitar uma configuração que gere parcela sem valor positivo.

Se o desconto à vista já reduziu a venda faturada, ele não deve ser aplicado novamente no recebimento.

## 11. Contas a receber, encargos e recebimentos

Uma venda gera um título por parcela. Venda ou nota 123, dividida em duas parcelas, pode ser apresentada como 123/1 e 123/2. A identificação interna permanece própria e única, independentemente da existência de nota fiscal.

O título guarda:

- Venda de origem e identificação fiscal quando disponível.
- Número da parcela.
- Valor original e vencimento.
- Forma prevista de pagamento.
- Cópia das taxas e regras aplicadas na venda.

Cada recebimento registra data, valor, meio efetivamente utilizado e responsável, discriminando principal, juros, multa e desconto. A implementação realiza quitação integral de cada título; pagamentos parciais são uma evolução, não uma funcionalidade entregue.

Saldo principal = valor original menos o principal já amortizado.

Valor para quitação em determinada data = saldo principal + encargos ainda devidos − descontos aplicáveis.

A dívida exibida no aplicativo deve informar a data da consulta. Proposta: separar principal em aberto, principal vencido e valor para quitação calculado na data. Não apresentar um valor antigo como saldo atualizado em tempo real.

### Multa, juros e antecipação

O professor exemplificou multa de 5%, juros de 2% ao dia e desconto de 1% ao dia. São parâmetros ilustrativos para o exercício, não valores fixos a serem impostos a todas as condições.

Decisões adotadas no cálculo acadêmico:

- Multa única sobre o principal, somente após o vencimento.
- Juros simples por dia corrido sobre o principal da parcela.
- Desconto diário por antecipação, limitado a 10% do principal.
- Quitação integral: principal e encargos são discriminados no recebimento.
- Sem desconto à vista adicional aplicado ao total da venda; evita concessão duplicada.
- Estorno da venda somente antes da entrega e sem recebimentos; devolve o estoque e cancela os títulos. Estorno de recebimento e devolução posterior à entrega ficam fora desta versão.

Essas são escolhas explicitadas da implementação, não novas exigências atribuídas ao professor. Os parâmetros de taxas continuam editáveis nas condições de pagamento.

A consulta calcula os encargos devidos. Consultar a dívida duas vezes não lança duas multas.

O professor dispensou limite de crédito na primeira versão. Não incluir bloqueio automático por limite de crédito. O gestor pode considerar pontualidade para decidir o corte, conforme explicado em aula.

## 12. Integridade e responsabilidade pelos dados

- Cadastros possuem status ativo/inativo; no mínimo, clientes, produtos e vendedores.
- Inativar preserva pedidos, vendas e títulos históricos.
- O vendedor vê somente a carteira autorizada pela API.
- Cliente e pedido offline possuem identificação estável para sincronização.
- O servidor valida vínculos, preços e estados; não aceita totais da tela como verdade.
- Venda, estoque e parcelas são confirmados em conjunto.
- Eventos guardam instante e responsável; validade e vencimento guardam datas.
- Dinheiro usa precisão exata. Proposta física para SQLite: armazenar centavos inteiros; cálculos em C# com decimal.
- Recebimentos e movimentos não são apagados para esconder correções; usar reversão rastreável.
- Aplicativo mostra a data de atualização dos dados que podem ter mudado no servidor.

## 13. Decisões e limites finais desta versão

A fala já definiu roteiros por dia, parcelamento, formas de pagamento e ausência de limite de crédito inicial. Esses pontos deixaram de ser dúvidas.

As decisões para fechar o fluxo local são:

1. Tecnologias definidas: .NET 10, aplicativo MAUI, site MVC e API; não há projeto base obrigatório.
2. Emissão fiscal definida: apenas simulação acadêmica.
3. Preço ou versão alterados enquanto offline geram rejeição para revisão, preservando o pedido local.
4. Encargos conforme §11; recebimento integral por parcela. Pagamentos parciais não são implementados.
5. Quantidade cortada é encerrada, sem reposição automática.
6. Quantidades aceitam até três casas decimais, com armazenamento em milésimos.
7. Envio confirmado pelo botão; 12h e 19h são referências e não bloqueiam envio em outro horário.
8. Condição alterada após recebimento exige cancelar/refazer o pedido antes de faturar; uma venda já emitida preserva suas regras.

Integração fiscal/bancária, publicação pública, aparelho físico, pagamentos parciais e devolução após entrega exigem etapas próprias; não são apresentados como parte da demonstração local concluída.



### Pontualidade na análise do pedido

A análise do corte apresenta principal em aberto, principal vencido, pagamentos pontuais, pagamentos com atraso, títulos vencidos e o maior atraso observado. A classificação usa vencimento e data efetiva do recebimento. Um pagamento sem data registrada não é presumido pontual. Os títulos cancelados são excluídos. A decisão de distribuição continua manual.

### Descarte e cancelamento no aplicativo

O descarte retira da fila apenas rascunhos, rejeitados ou operações locais prontas, conservando o registro no SQLite como Descartado. O cadastro de cliente não pode ser descartado enquanto possui pedido dependente. Estados de envio incerto não podem ser descartados. Um pedido recebido pela central utiliza a operação de cancelamento online, com motivo, versão atual e autorização da carteira; faturamento já ocorrido impede esse cancelamento.
