namespace Aixaminator
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage()) { Title = "Aixaminator", Width = 1280, Height = 768 };
        }
    }
}
