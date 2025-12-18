using System;
using System.Collections.Generic;
using System.Globalization;

namespace BluetoothManager.Helpers
{
    public static class StringResources
    {
        private static string _currentLanguage = "es-ES";

        private static Dictionary<string, Dictionary<string, string>> _resources = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "en-US", new Dictionary<string, string>
                {
                    { "WindowTitle", "Bluetooth Manager" },
                    { "ButtonRefresh", "Refresh" },
                    { "SearchPlaceholder", "Search by name or MAC..." },
                    { "ButtonClearSaved", "Delete saved names" },
                    { "ColumnName", "Name" },
                    { "ColumnFriendlyName", "Friendly Name" },
                    { "ColumnStatus", "Status" },
                    { "ColumnActions", "Actions" },
                    { "ButtonPair", "Pair" },
                    { "ButtonUnpair", "Unpair" },
                    { "ButtonSaveName", "Save name" },
                    { "StatusListingDevices", "Listing devices..." },
                    { "StatusDevicesListed", "devices listed." },
                    { "StatusPairing", "Pairing {0}..." },
                    { "StatusPaired", "Paired" },
                    { "StatusErrorPairing", "Error pairing: {0}" },
                    { "StatusUnpairing", "Unpairing {0}..." },
                    { "StatusUnpaired", "Unpaired" },
                    { "StatusErrorUnpairing", "Error unpairing: {0}" },
                    { "StatusNameSaved", "Name saved" },
                    { "StatusErrorSavingName", "Error saving name" },
                    { "StatusNamesClearedError", "Error deleting names" },
                    { "StatusNamesCleared", "Saved names deleted" },
                    { "ErrorPairingTitle", "Error" },
                    { "ErrorPairingMessage", "Error pairing {0}:\n\n{1}" },
                    { "ErrorUnpairingMessage", "Error unpairing {0}:\n\n{1}" },
                    { "DeviceStatusSaved", "Saved" }
                }
            },
            {
                "es-ES", new Dictionary<string, string>
                {
                    { "WindowTitle", "Gestor Bluetooth" },
                    { "ButtonRefresh", "Refrescar" },
                    { "SearchPlaceholder", "Buscar por nombre o MAC..." },
                    { "ButtonClearSaved", "Eliminar nombres guardados" },
                    { "ColumnName", "Nombre" },
                    { "ColumnFriendlyName", "Nombre dispositivo" },
                    { "ColumnStatus", "Estado" },
                    { "ColumnActions", "Acciones" },
                    { "ButtonPair", "Emparejar" },
                    { "ButtonUnpair", "Desemparejar" },
                    { "ButtonSaveName", "Guardar nombre" },
                    { "StatusListingDevices", "Listando dispositivos..." },
                    { "StatusDevicesListed", "dispositivos listados." },
                    { "StatusPairing", "Emparejando {0}..." },
                    { "StatusPaired", "Emparejado" },
                    { "StatusErrorPairing", "Error emparejando: {0}" },
                    { "StatusUnpairing", "Desemparejando {0}..." },
                    { "StatusUnpaired", "Desemparejado" },
                    { "StatusErrorUnpairing", "Error desemparejando: {0}" },
                    { "StatusNameSaved", "Nombre guardado" },
                    { "StatusErrorSavingName", "Error guardando nombre" },
                    { "StatusNamesClearedError", "Error eliminando nombres" },
                    { "StatusNamesCleared", "Nombres guardados eliminados" },
                    { "ErrorPairingTitle", "Error" },
                    { "ErrorPairingMessage", "Error emparejando {0}:\n\n{1}" },
                    { "ErrorUnpairingMessage", "Error desemparejando {0}:\n\n{1}" },
                    { "DeviceStatusSaved", "Guardado" }
                }
            }
        };

        public static void SetLanguage(string languageCode)
        {
            if (_resources.ContainsKey(languageCode))
            {
                _currentLanguage = languageCode;
                var culture = CultureInfo.GetCultureInfo(languageCode);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }
        }

        public static string GetString(string key)
        {
            if (_resources.ContainsKey(_currentLanguage) && _resources[_currentLanguage].ContainsKey(key))
            {
                return _resources[_currentLanguage][key];
            }
            // Fallback to English if key not found
            if (_resources.ContainsKey("en-US") && _resources["en-US"].ContainsKey(key))
            {
                return _resources["en-US"][key];
            }
            return key;
        }

        public static string GetCurrentLanguage() => _currentLanguage;
    }
}
