using System.Text.RegularExpressions;

namespace LogCollectorApp.Helpers
{
    /// <summary>
    /// Вспомогательный класс для валидации и преобразования масок IP-адресов.
    /// Позволяет пользователю искать серверы по маске (например, 192.168.1.*)
    /// </summary>
    public static class IpMaskHelper
    {
        /// <summary>
        /// Регулярное выражение для проверки корректности маски IP-адреса.
        /// Разрешает цифры от 0 до 255 и символ '*' в качестве маски.
        /// Примеры валидных масок: "192.168.1.*", "10.0.*.*", "172.16.0.1"
        /// </summary>
        private static readonly Regex IpMaskRegex = new Regex(
            @"^(\d{1,3}|\*)\.(\d{1,3}|\*)\.(\d{1,3}|\*)\.(\d{1,3}|\*)$",
            RegexOptions.Compiled
        );

        /// <summary>
        /// Проверяет, является ли строка валидной маской IP-адреса.
        /// </summary>
        /// <param name="mask">Строка с маской (например, "192.168.1.*")</param>
        /// <returns>True, если маска валидна, иначе False</returns>
        public static bool IsValidIpMask(string mask)
        {
            if (string.IsNullOrWhiteSpace(mask))
                return false;

            if (!IpMaskRegex.IsMatch(mask))
                return false;

            // Дополнительная проверка: если это не звездочка, число должно быть от 0 до 255
            string[] parts = mask.Split('.');
            foreach (string part in parts)
            {
                if (part == "*") continue;

                if (int.TryParse(part, out int number))
                {
                    if (number < 0 || number > 255)
                        return false;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Преобразует маску IP в SQL LIKE паттерн для запроса к PostgreSQL.
        /// Заменяет символ '*' на символ '%', который используется в SQL для поиска по шаблону.
        /// </summary>
        /// <param name="mask">Валидная маска IP (например, "192.168.1.*")</param>
        /// <returns>SQL LIKE паттерн (например, "192.168.1.%")</returns>
        public static string ConvertToSqlLikePattern(string mask)
        {
            if (string.IsNullOrWhiteSpace(mask))
                return "%";

            // В PostgreSQL для LIKE используется символ '%' вместо '*'
            return mask.Replace("*", "%");
        }
    }
}