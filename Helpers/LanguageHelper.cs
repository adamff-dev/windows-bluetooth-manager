namespace BluetoothManager.Helpers
{
    public static class LanguageHelper
    {
        /// <summary>
        /// Set application language to Spanish (es-ES)
        /// </summary>
        public static void SetSpanish()
        {
            StringResources.SetLanguage("es-ES");
        }

        /// <summary>
        /// Set application language to English (en-US)
        /// </summary>
        public static void SetEnglish()
        {
            StringResources.SetLanguage("en-US");
        }

        /// <summary>
        /// Set application language to a specific culture
        /// </summary>
        public static void SetLanguage(string languageCode)
        {
            StringResources.SetLanguage(languageCode);
        }

        /// <summary>
        /// Get current language code (e.g., "es-ES" or "en-US")
        /// </summary>
        public static string GetCurrentLanguage() => StringResources.GetCurrentLanguage();
    }
}
