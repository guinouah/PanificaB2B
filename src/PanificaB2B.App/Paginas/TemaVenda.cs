using Microsoft.Maui.Controls.Shapes;

namespace PanificaB2B.App.Paginas;

// Componentes visuais compartilhados; regras e persistencia permanecem nos servicos existentes.
public static class TemaVenda
{
    public static readonly Color Verde=Color.FromArgb("#183F35");
    public static readonly Color Fundo=Color.FromArgb("#F4F5EF");
    public static readonly Color Texto=Color.FromArgb("#263C33");
    public static readonly Color Suave=Color.FromArgb("#687B6B");
    public static readonly Color Linha=Color.FromArgb("#E0E7DC");
    public static readonly Color VerdeClaro=Color.FromArgb("#EAF0E5");
    public static Label Legenda(string texto)=>new(){Text=texto,FontSize=12,TextColor=Suave,LineBreakMode=LineBreakMode.WordWrap};
    public static Label Titulo(string texto,double tamanho=22)=>new(){Text=texto,FontFamily="OpenSansSemibold",FontSize=tamanho,TextColor=Texto,LineBreakMode=LineBreakMode.WordWrap};
    public static Border Cartao(params View[] filhos)
    {
        var layout=new VerticalStackLayout{Spacing=10};
        foreach(var filho in filhos)layout.Children.Add(filho);
        return new Border{BackgroundColor=Colors.White,Stroke=new SolidColorBrush(Linha),StrokeShape=new RoundRectangle{CornerRadius=16},Padding=18,Content=layout};
    }
    public static View Cabecalho(string categoria,string titulo,string descricao)
    {
        return new VerticalStackLayout{Spacing=6,Children={
            new Label{Text=categoria.ToUpperInvariant(),FontSize=10,CharacterSpacing=1.8,TextColor=Suave,FontFamily="OpenSansSemibold"},
            Titulo(titulo,26),Legenda(descricao)}};
    }
    public static Border Etiqueta(string texto,bool promocao=false,bool azul=false)=>new()
    {
        HorizontalOptions=LayoutOptions.Start,StrokeThickness=0,Padding=new Thickness(9,4),StrokeShape=new RoundRectangle{CornerRadius=6},
        BackgroundColor=promocao?Color.FromArgb("#FFF0BD"):azul?Color.FromArgb("#E4EFFA"):VerdeClaro,
        Content=new Label{Text=texto,FontSize=10,FontFamily="OpenSansSemibold",TextColor=promocao?Color.FromArgb("#795813"):azul?Color.FromArgb("#315D85"):Verde}
    };
    public static View Campo(string titulo,View campo)=>new VerticalStackLayout{Spacing=4,Children={new Label{Text=titulo,FontSize=12,FontFamily="OpenSansSemibold",TextColor=Texto},campo}};
    public static Grid Colunas(View a,View b)
    {
        var grid=new Grid{ColumnDefinitions={new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Star)},ColumnSpacing=10};
        grid.Add(a,0,0);grid.Add(b,1,0);return grid;
    }
    public static View Numero(string valor,string titulo)=>Cartao(Titulo(valor,24),Legenda(titulo));
    public static VerticalStackLayout Corpo()=>new(){Padding=new Thickness(20,20,20,32),Spacing=18,MaximumWidthRequest=900,HorizontalOptions=LayoutOptions.Fill};
    public static ScrollView Pagina(VerticalStackLayout corpo)=>new(){Content=corpo};
    public static View Vazio(string titulo,string descricao)=>Cartao(Titulo(titulo,17),Legenda(descricao));
}
