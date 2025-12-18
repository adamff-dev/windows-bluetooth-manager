namespace BluetoothManager.Models
{
    public class Device
    {
        public string FriendlyName { get; set; }
        public string Status { get; set; }
        public string MAC { get; set; }
        public string Address { get; set; }
        public string DisplayName { get; set; }
        public bool Known { get; set; }
    }
}