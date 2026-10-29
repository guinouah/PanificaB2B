using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PanificaB2B.App.Dados;
using PanificaB2B.App.Paginas;
using PanificaB2B.Contratos;

namespace PanificaB2B.App;

public partial class MainPage : ContentPage
{
    const string ChaveSessao = "panifica.sessao";
    readonly CarteiraLocal local = new(Path.Combine(FileSystem.AppDataDirectory, "vendedor.db3"));
    readonly TrabalhoLocal trabalho = new(Path.Combine(FileSystem.AppDataDirectory, "vendedor.db3"));
    readonly HttpClient api = new() { Timeout = TimeSpan.FromSeconds(15) };
    static readonly string[] dias = ["Domingo", "Segunda-feira", "Terça-feira", "Quarta-feira", "Quinta-feira", "Sexta-feira", "Sábado"];
    SessaoDispositivo? sessao;
    bool iniciado;
    public MainPage()
    {
        InitializeComponent();
#if ANDROID
        EnderecoApi.Text = "http://10.0.2.2:5187/";
#endif
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (iniciado) return;
        iniciado = true;
        try
        {
            var salvo = await SecureStorage.Default.GetAsync(ChaveSessao);
            sessao = salvo is null ? null : JsonSerializer.Deserialize<SessaoDispositivo>(salvo);
            if (sessao?.Usuario.Perfil == PerfilUsuario.Vendedor && sessao.Usuario.VendedorId is { } vendedorId)
            {
                EnderecoApi.Text = sessao.ApiUrl;
                LoginEntry.Text = sessao.Usuario.Login;
                UsuarioLabel.Text = $"Vendedor: {sessao.Usuario.Login}";
                if (await local.Carregar(vendedorId) is { } dados) Mostrar(dados);
                if (sessao.ExpiraEm <= DateTimeOffset.UtcNow) SituacaoLabel.Text += "\nEntre novamente para atualizar pela internet.";
            }
            else LimparSessao();
        }
        catch (Exception) { LimparSessao(); SituacaoLabel.Text = "Entre novamente para identificar sua carteira."; }
        Habilitar(true);
    }
    Uri Raiz()
    {
        var endereco = sessao?.ApiUrl ?? EnderecoApi.Text;
        if (!Uri.TryCreate(endereco?.TrimEnd('/') + "/", UriKind.Absolute, out var root) || root.Scheme is not ("http" or "https"))
            throw new ArgumentException("Informe um endereço HTTP ou HTTPS válido.");
        return root;
    }
    HttpRequestMessage Autorizado(HttpMethod metodo, string route)
    {
        if (sessao is null || sessao.ExpiraEm <= DateTimeOffset.UtcNow) throw new InvalidOperationException("Entre novamente para atualizar sua carteira.");
        var request = new HttpRequestMessage(metodo, new Uri(Raiz(), route));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessao.Token);
        return request;
    }
    void Habilitar(bool ativo)
    {
        EntrarButton.IsEnabled = ativo;
        SairButton.IsEnabled = ativo && sessao is not null;
        TrabalharButton.IsEnabled = ativo && sessao is not null;
        BaixarButton.IsEnabled = ativo && sessao is not null && sessao.ExpiraEm > DateTimeOffset.UtcNow;
        EnderecoApi.IsEnabled = ativo && sessao is null;
        LoginEntry.IsEnabled = ativo;
        SenhaEntry.IsEnabled = ativo;
    }
    async void Entrar(object? sender, EventArgs e)
    {
        Habilitar(false);
        try
        {
            var root = Raiz();
            using var result = await api.PostAsJsonAsync(new Uri(root, "api/auth/entrar"), new CredenciaisEntrada { Login = LoginEntry.Text ?? "", Senha = SenhaEntry.Text ?? "" });
            if (!result.IsSuccessStatusCode) { SituacaoLabel.Text = result.StatusCode == HttpStatusCode.TooManyRequests ? "Muitas tentativas. Aguarde um minuto." : "Login ou senha inválidos."; return; }
            var token = (await result.Content.ReadFromJsonAsync<TokenResposta>())!;
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(root, "api/auth/eu"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            using var userResult = await api.SendAsync(request);
            userResult.EnsureSuccessStatusCode();
            var user = (await userResult.Content.ReadFromJsonAsync<UsuarioDto>())!;
            if (user.Perfil != PerfilUsuario.Vendedor || user.VendedorId is null)
            {
                using var revogar = new HttpRequestMessage(HttpMethod.Post, new Uri(root, "api/auth/sair"));
                revogar.Headers.Authorization = request.Headers.Authorization;
                using var ignored = await api.SendAsync(revogar);
                SituacaoLabel.Text = "Utilize um acesso de vendedor. A equipe da distribuidora acessa pelo site.";
                return;
            }
            var nova = new SessaoDispositivo(user, root.AbsoluteUri, token.AccessToken, DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn));
            // Do not retain passwords. SecureStorage holds the validated identity/token for this device.
            await SecureStorage.Default.SetAsync(ChaveSessao, JsonSerializer.Serialize(nova));
            sessao = nova;
            CarteiraLista.Children.Clear(); RoteirosLista.Children.Clear();
            UsuarioLabel.Text = $"Vendedor: {user.Login}";
            SituacaoLabel.Text = "Acesso confirmado. Baixe sua carteira.";
            if (await local.Carregar(user.VendedorId.Value) is { } dados) Mostrar(dados);
        }
        catch (Exception) { SituacaoLabel.Text = "Não foi possível entrar. Confira a conexão e o endereço da API."; }
        finally { SenhaEntry.Text = ""; Habilitar(true); }
    }
    void LimparSessao()
    {
        sessao = null;
        SecureStorage.Default.Remove(ChaveSessao);
        CarteiraLista.Children.Clear(); RoteirosLista.Children.Clear();
        UsuarioLabel.Text = "";
        SenhaEntry.Text = "";
    }
    async void Sair(object? sender, EventArgs e)
    {
        Habilitar(false);
        try
        {
            using var request = Autorizado(HttpMethod.Post, "api/auth/sair");
            using var response = await api.SendAsync(request);
        }
        catch (Exception) { /* Offline exit still removes the local session. */ }
        finally { LimparSessao(); SituacaoLabel.Text = "Você saiu do sistema."; Habilitar(true); }
    }
    async void BaixarCarteira(object? sender, EventArgs e)
    {
        if (sessao?.Usuario.VendedorId is not { } vendedorId) return;
        Habilitar(false);
        try
        {
            using var request = Autorizado(HttpMethod.Get, $"api/vendedores/{vendedorId}/preparacao");
            using var result = await api.SendAsync(request);
            if (result.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                LimparSessao(); SituacaoLabel.Text = "Acesso encerrado ou alterado. Entre novamente."; return;
            }
            result.EnsureSuccessStatusCode();
            var dados = await result.Content.ReadFromJsonAsync<PreparacaoDto>() ?? throw new InvalidDataException();
            if (dados.VendedorId != vendedorId) throw new InvalidDataException();
            await local.Salvar(dados); Mostrar(dados);
        }
        catch (Exception) { SituacaoLabel.Text = "Falha no download. A carteira salva foi mantida; entre novamente se a sessão expirou."; }
        finally { Habilitar(true); }
    }
    void Mostrar(PreparacaoDto dados)
    {
        CarteiraLista.Children.Clear(); RoteirosLista.Children.Clear();
        SituacaoLabel.Text = $"Dados baixados em {dados.AtualizadoEm.ToLocalTime():dd/MM/yyyy HH:mm}. Disponíveis sem conexão.";
        foreach (var cliente in dados.Clientes.OrderBy(c => c.Nome)) CarteiraLista.Children.Add(new Label { Text = $"{cliente.Nome}\n{cliente.Endereco}", Padding = new Thickness(0, 6) });
        if (dados.Clientes.Count == 0) CarteiraLista.Children.Add(new Label { Text = "Nenhum cliente ativo nesta carteira." });
        foreach (var roteiro in dados.Roteiros.OrderBy(r => r.DiaSemana).ThenBy(r => r.Nome))
        {
            RoteirosLista.Children.Add(new Label { Text = $"{dias[roteiro.DiaSemana]} · {roteiro.Nome}", FontAttributes = FontAttributes.Bold });
            foreach (var visita in roteiro.Visitas.OrderBy(v => v.Sequencia)) RoteirosLista.Children.Add(new Label { Text = $"{visita.Sequencia} — {visita.NomeCliente}" });
        }
    }
    async void AbrirTrabalho(object? sender, EventArgs e)
    {
        if (sessao?.Usuario.VendedorId is not { } id) return;
        try
        {
            var dados = await local.Carregar(id);
            if (dados is null) { await DisplayAlertAsync("Preparação", "Baixe sua carteira antes de trabalhar offline.", "OK"); return; }
            await Navigation.PushAsync(new OperacaoPage(sessao, dados, local, trabalho));
        }
        catch (Exception ex) { await DisplayAlertAsync("Carteira", ex.Message, "OK"); }
    }
}

