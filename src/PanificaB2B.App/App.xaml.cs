using Microsoft.Extensions.DependencyInjection;

namespace PanificaB2B.App;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
        UserAppTheme = AppTheme.Light;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}