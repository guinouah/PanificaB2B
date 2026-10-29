# Apresentação e cadastros válidos — 28/10/2026

- 217 verificações integradas passaram na saída normal e em compilação isolada: seletores, rejeição da forma numérica 8 no site/API, estado inválido, município de outra UF, demonstração idempotente, histórico de pontualidade no corte, cancelamento pelo vendedor e descarte local com proteção de dependências/envio incerto.
- API, contratos, site MVC e integração compilados sem avisos ou erros. Aplicativo Windows e APK Android atualizados e compilados sem avisos ou erros; assinatura do APK verificada com v1/v2/v3.
- JavaScript real dos municípios verificado: seleção inicial preservada, troca de UF elimina município anterior e ausência de UF bloqueia a cidade.
- Cenário de demonstração carregado no banco local após backup consistente: três pedidos pendentes 5/8/9, fermento com saldo 15 e acessos por perfil. A inclusão preservou os dados anteriores. Não foi publicado banco nem senha.
- Catálogo de localidades obtido da API oficial do IBGE em 28/10/2026, incluído como recurso local. Não há dependência de internet para selecionar UFs/municípios na apresentação.
- A API e o site chegaram a iniciar em sessões locais controladas; a tentativa de acesso pelo navegador do agente terminou em timeout. Não foi comprovado teste visual novo nem execução do aplicativo em aparelho/emulador. A conferência manual pelo usuário continua necessária.
- Os registros detalhados e as credenciais dos acessos preparados permanecem somente nas pastas locais ignoradas.

# Atualização da interface — 28/10/2026

- Site MVC com navegação lateral por perfil, acesso renovado, painel de indicadores, busca de clientes/pedidos e ficha financeira do cliente.
- Pedidos: demanda agrupada por produto com quantidades por cliente, estoque válido e falta; corte individual com prévia do valor, quantidade cortada e motivo obrigatório quando há redução.
- Compilação de contratos, API, MVC e integração: zero avisos e erros.
- 205 verificações integradas passaram em base isolada. O formulário MVC foi exercitado com pedidos de 5/8/9 e cortes de 2/6/7: três vendas somando R$ 150, seis parcelas e estoque final zero. Atendimento acima do pedido e corte sem motivo foram recusados.
- O JavaScript real foi verificado em ambiente de teste de DOM: prévia integral/parcial/zero, quantidade inválida, aviso de estoque, arredondamento de meio centavo e busca sem acentos.
- Testes de renderização e submissão usam TestServer. A conferência visual no navegador e no dispositivo continua pendente; não foi comprovada por captura de tela. Não houve alteração nas configurações do Windows.
- Contas de apresentação, bancos, backups e credenciais permanecem apenas nas pastas locais ignoradas. As senhas utilizadas nos testes automatizados são aleatórias.

# Atualização de permissões — 28/10/2026

- Cinco perfis com permissões da API, menus e páginas separados.
- 193 verificações integradas passaram com base nova, incluindo bloqueios por departamento e migração da base antiga com preservação de usuários, hashes e referências.
- Novos acessos exigem 12 caracteres; usuários existentes mantêm suas credenciais.
- Registros detalhados permanecem locais e são excluídos do Git por poderem conter caminhos da máquina. Os resultados abaixo de 23/10 são históricos; a interface no dispositivo continua pendente de teste manual.

# Validação da entrega — 23/10/2026

## Resultado

| Verificação | Resultado |
|---|---|
| API, contratos, site MVC e integração | Compilados, zero avisos e erros |
| Integração com banco novo | 131 verificações passaram |
| Integração com cópia da base da primeira etapa | 139 verificações passaram, incluindo preservação das oito tabelas originais |
| Cache SQLite real do aplicativo | 7 verificações passaram |
| Regras básicas de acesso | 8 verificações passaram |
| Aplicativo MAUI Windows x64 | Compilado, zero avisos e erros; Windows App SDK incluído na saída |
| Aplicativo MAUI Android | APK Debug compilado, zero avisos e erros; assemblies da aplicação incluídos |
| Assinatura do APK | apksigner verificou com sucesso v1, v2 e v3 |
| Integridade dos bancos de teste | integrity_check e foreign_key_check passaram |

Os números das suítes nova/antiga incluem cenários compartilhados e não representam testes distintos para somar entre si.

## Cenários exercitados

- Primeiro administrador, login, senhas como hash, perfis, carteira, revogação, expiração e usuário/vendedor inativo.
- Grupos únicos, cadastro/edição/inativação, conflito de versão e atualização do banco repetida.
- Produtos, unidade, preço exato, promoção vigente e novidade.
- Formas de pagamento, parcelas por percentual e regras financeiras.
- Entradas por lote, repetição sem duplicação, lotes vencidos e perdas rastreadas.
- Pedidos de 5, 8 e 9 unidades; atendimento de 2, 6 e 7; cobrança de somente 15 unidades.
- Corte maior que o pedido ou estoque é rejeitado; corte total não gera venda/cobrança.
- Falha no segundo item reverte venda, estoque e parcelas.
- Falha na geração de parcela sem valor positivo também reverte o estoque já processado.
- Duas requisições simultâneas sobre a última unidade: somente uma venda, saldo final zero.
- Confirmação repetida não duplica venda, baixa ou títulos.
- Idempotência normaliza escala decimal e o fuso do mesmo instante.
- Última compra com quantidade realmente atendida; venda estornada fora desse histórico.
- Venda de R$ 235: duas parcelas de R$ 117,50; três parcelas de R$ 78,33/R$ 78,33/R$ 78,34.
- Prazos desde a data da venda, multa única, juros simples, desconto e preservação das taxas históricas.
- Recebimento integral, forma real diferente da prevista, repetição segura e redução da dívida.
- Entrega e estorno com bloqueio para venda entregue ou com recebimento.
- Download autorizado com preços, condições, compras e dívida, omitindo estoque.
- Código real do SQLite/fila do aplicativo contra API real: cliente primeiro, rascunho não enviado, resposta perdida após commit, reenvio sem duplicata, falha individual, reinício do acesso local e isolamento por vendedor.
- HTML das telas administrativas e formulários reais de produto, estoque, pedido, corte, recebimento, condição, localidades, vendedor, território, roteiro e cliente.
- CSS público antes do login e conversão decimal em formulários em português.

## Artefatos

- Windows: `src/PanificaB2B.App/bin/Debug/net10.0-windows10.0.19041.0/win-x64/PanificaB2B.App.exe`.
- Android: `src/PanificaB2B.App/bin/Debug/net10.0-android/com.companyname.panificab2b.app-Signed.apk`.
- Arquiteturas Android verificadas no pacote: arm64-v8a, x86_64.
- Tamanho do APK: 86989479 bytes.
- SHA-256: `419ccf2b5e883e67f6d3b41c65e33a22982497c91f82ca093c65609406193351`.

A inspeção do ZIP confirmou os assemblies PanificaB2B.App e PanificaB2B.Contratos embarcados. A assinatura foi verificada com o apksigner do SDK Android instalado. É um pacote de desenvolvimento, sem publicação em loja.

## Método e limites

API e site executaram em TestServer dentro do processo dos testes. Os formulários utilizaram autenticação, cookies e proteção antifalsificação reais; a fila offline utilizou seu código de produção e um transporte em memória para a API. A perda de resposta foi injetada após o commit, antes de o aplicativo receber a confirmação.

Nenhuma credencial real foi usada. Os testes trabalharam em bancos isolados e cópias, sem apagar ou modificar o banco fornecido como origem.

A interface MAUI não foi executada em aparelho/emulador nesta validação. A compilação e os testes de lógica não comprovam a experiência visual no dispositivo. O roteiro manual está em GUIA-DE-TESTE.md. O site atualizado também deve ser aberto no navegador do usuário após reiniciar os processos, porque o ambiente do agente não conseguiu estabelecer essas conexões locais.

Não foram alterados configurações permanentes, firewall, política de scripts ou certificados de confiança do Windows. Os contornos de SDK/pasta temporária foram parâmetros e variáveis somente dos processos de compilação. Não foram instalados SDKs ou workloads.

## Registros locais

Os arquivos detalhados de validação são mantidos localmente em `validacao/` e não entram no repositório público.
