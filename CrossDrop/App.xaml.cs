namespace CrossDrop;

public partial class App
{
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();
    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        var window = base.CreateWindow(activationState);

        const int width = 800;
        const int height = 600;

        window.Width = width;
        window.Height = height;

        window.MinimumWidth = width;
        window.MinimumHeight = height;
        
        window.MaximumWidth = width;
        window.MaximumHeight = height;

        window.Title = "CrossDrop";

        return window;
    }
}