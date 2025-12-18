using System.Windows;
using BluetoothManager.Helpers;

namespace BluetoothManager
{
    public partial class App : Application
    {
        public App()
        {
            // Set to Spanish by default
            StringResources.SetLanguage("es-ES");
        }
    }
}