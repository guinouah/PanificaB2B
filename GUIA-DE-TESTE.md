# Roteiro de demonstração — Panifica B2B

## Apresentação rápida com dados prontos

1. Entre com um administrador e, em **Visão geral → Preparar dados para apresentação**, defina a senha dos acessos de demonstração. Se a demonstração já foi preparada neste computador, os dados e atendimentos serão mantidos.
2. Em **Pedidos e cortes**, abra o fermento identificado por **[DEMO]**: solicitado **22**, disponível **15**, falta **7**.
3. Abra cada cliente e explique **Pontualidade do cliente**: Aurora pagou no prazo; Doce Trigo pagou com atraso; Pão da Vila tem parcela vencida.
4. Atenda **2**, **6** e **7**, respectivamente, informando o motivo do corte. Cada confirmação reduz o estoque; o total entregue é **15 unidades** e **R$ 150**. Os pedidos históricos da demonstração são separados destes três pedidos pendentes.
5. Mostre as vendas e as seis parcelas geradas pela condição em 7 e 14 dias.
6. Em **Pagamentos**, confira que a nova forma exige selecionar uma opção. Em **Cadastros**, selecione UF e município. A API também recusa opções inválidas.
7. No aplicativo, entre como **vendedor.demo** com a senha definida na preparação, atualize a carteira e confira o roteiro. Para testar cancelamento online, use um pedido recebido que ainda não tenha sido faturado. Para testar descarte offline, crie um rascunho e use **Descartar pedido**.
8. A execução em aparelho/emulador e a verificação visual continuam sendo etapas manuais; as verificações automatizadas não substituem esses testes.

## 1. Abrir e preparar

1. Encerre as janelas antigas da API e do site com Ctrl+C.
2. Execute `testar-local.cmd` na pasta da solução. Aguarde **Now listening on** nas duas janelas.
3. Acesse `http://localhost:5188` e entre com o administrador já cadastrado. Se for uma base nova, crie o primeiro administrador.
4. Em **Cadastros**, confira estados, cidades, vendedores e territórios. A base inicial possui dados fictícios; você pode manter os seus.
5. Em **Clientes**, confira a cidade e o território. Em **Roteiros**, organize as visitas 10, 20 e 30; teste uma inserção com 15.

Os formulários novos não apagam os registros já existentes. Para inativar, abra o cadastro e desmarque Ativo.

## 2. Catálogo e pagamento

1. Em **Grupos**, cadastre Fermentos, caso ainda não exista.
2. Em **Produtos**, cadastre código FER-01, nome Fermento, unidade UN, grupo Fermentos e preço R$ 10,00.
3. Opcionalmente, configure novidade até uma data futura. Em outro produto, configure preço promocional menor que o normal e um período incluindo hoje. Confira os destaques.
4. Em **Pagamentos**, cadastre Pix e Boleto como formas.
5. Crie a condição código 7-14, nome 7 e 14 dias: duas parcelas de 50%, vencendo em 7 e 14 dias. A primeira pode ser Pix e a segunda Boleto. Deixe as taxas zeradas para o primeiro teste.

## 3. Estoque e exemplo de corte

1. Em **Estoque → Registrar entrada**, dê entrada em 15 unidades do fermento, lote TESTE-01, com validade futura.
2. Use três clientes da mesma carteira ou cadastre os que faltarem.
3. Em **Pedidos → Novo pedido no site**, faça três pedidos: A pede 5, B pede 8 e C pede 9 unidades, utilizando a condição 7-14.
4. A demanda pendente deverá mostrar 22 solicitadas, 15 disponíveis e falta de 7.
5. Abra cada pedido e defina os atendimentos: A recebe 2, B recebe 6 e C recebe 7. Informe o motivo do corte e confirme cada faturamento.
6. Confira as vendas de R$ 20,00, R$ 60,00 e R$ 70,00: total de R$ 150,00. O saldo do lote deverá ficar zero.
7. Confira que a quantidade pedida permanece 5/8/9 e a atendida permanece 2/6/7.
8. Em **Financeiro**, confira duas parcelas por venda, com a soma igual ao total efetivamente faturado.

Não precisa esperar nenhum horário para fazer o pedido ou enviá-lo; 12h e 19h são referências para o botão de envio.

## 4. Aplicativo e sincronização offline

1. Em **Acessos** no site, crie login/senha de perfil Vendedor, vinculado ao cadastro comercial da carteira utilizada. A senha deve ter pelo menos doze caracteres.
2. Execute `testar-aplicativo.cmd` para Windows ou abra o projeto MAUI no Visual Studio com o destino desejado.
3. Entre como vendedor. No Windows, use a API `http://localhost:5187/`; no emulador Android, `http://10.0.2.2:5187/`.
4. Toque em **Atualizar carteira, preços, histórico e pedidos**.
5. Abra **Carteira, produtos e pedidos offline**. Confira roteiro, dívida datada, catálogo e a quantidade da última compra efetiva.
6. Para simular falta de internet sem alterar a máquina, encerre somente a janela da API. O SQLite local deve continuar permitindo consulta e salvamento.
7. Cadastre um novo cliente offline dentro de um território baixado e monte um pedido para ele. Salve um rascunho e outro pedido pronto para envio.
8. Na tela **Pedidos locais e envio**, tente enviar com a API desligada. A operação deve continuar pendente. O rascunho não entra no envio.
9. Reabra a API pelo comando abaixo, em um PowerShell na pasta da solução; mantenha o site já aberto:

```powershell
cd .\src\PanificaB2B.Api
dotnet .\bin\Debug\net10.0\PanificaB2B.Api.dll
```

10. No aplicativo, entre novamente se a sessão tiver expirado. Confirme o envio: o cliente novo é enviado antes do pedido.
11. No site, confira **Clientes** e **Pedidos**. Repetir o botão não deve duplicar os registros confirmados.
12. Faça o faturamento no site e volte ao aplicativo para atualizar a carteira. Confira situação, quantidade atendida, compras e dívida.

Atualizar a carteira não remove rascunhos nem pendências. Pedidos com confirmação incerta precisam ser reenviados antes de editar; pedidos rejeitados por preço antigo podem ser revisados após atualizar o catálogo.

## 5. Parcelamento e recebimento

1. Para o exemplo do professor, cadastre um produto de R$ 47,00, dê entrada de cinco unidades válidas e fature um pedido de cinco unidades: total R$ 235,00.
2. Com condição 7-14 de 50%/50%, confira duas parcelas de R$ 117,50, sete e catorze dias após a venda.
3. Com uma condição 33,33%/33,33%/33,34% em 7/14/21 dias, confira R$ 78,33, R$ 78,33 e R$ 78,34.
4. Abra uma parcela em **Financeiro → Receber**. Selecione a data e recalcule antes de confirmar. Escolha a forma efetivamente utilizada e confirme a baixa integral.
5. Confira o histórico do recebimento, a situação Pago e a redução da dívida do cliente.

Para testar encargos sem mudar o relógio do Windows, altere a **data de cálculo** na consulta do Financeiro. Consultar uma data futura é uma simulação; registrar recebimento futuro é recusado.

Regras do exercício: multa única e juros simples por dias corridos; desconto antecipado limitado a 10% da parcela. As taxas são configuradas na condição e copiadas para os títulos ao faturar.

## 6. Proteções para conferir

- Tente cadastrar dois grupos ou códigos de produto iguais: o sistema deve recusar.
- Digite atendimento maior que o pedido ou maior que o estoque válido: não deve faturar.
- Cadastre um lote vencido: aparece destacado e não pode abastecer uma venda.
- Corte tudo para zero: o pedido termina como Não atendido, sem venda nem títulos.
- Tente faturar duas vezes: não deve gerar outra venda.
- Atualize um formulário aberto em duas abas e salve a versão antiga: deve informar conflito.
- Altere o preço depois do download e tente enviar o pedido antigo: deve pedir revisão, preservando o conteúdo local.
- Entre como outro vendedor: a API não deve liberar a carteira, dívida ou pedidos do primeiro.
- Antes de confirmar entrega, estorne uma venda sem pagamento: saldo retorna aos lotes e títulos são cancelados.
- Venda entregue ou com recebimento não pode ser estornada neste módulo.

## Encerrar

Feche o aplicativo e pressione Ctrl+C nas janelas da API e do site. Os bancos permanecem salvos para a próxima abertura.
