using System;
using System.Collections.Generic;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace TimeTracker.Services
{
    /// <summary>
    /// Извлекает URL активной вкладки Edge через UI Automation:
    /// ищет в дереве окна Edit-контрол адресной строки и читает его ValuePattern.
    /// Запросы кэшируются (UIA-обход дерева — недешёвая операция), исключения глотаются.
    /// Firefox/Chrome — вне скоупа этой итерации, класс расширяем позже.
    /// </summary>
    public static class EdgeUrlProvider
    {
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(1.5);

        private static UIA3Automation? _automation;
        private static readonly object Lock = new();
        private static readonly Dictionary<IntPtr, (string? Url, DateTime At)> Cache = new();

        /// <summary>Возвращает URL активной вкладки окна Edge или null при любой ошибке.</summary>
        public static string? GetUrl(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return null;

            lock (Lock)
            {
                if (Cache.TryGetValue(hwnd, out var cached) && DateTime.Now - cached.At < CacheLifetime)
                    return cached.Url;

                string? url = null;
                try
                {
                    _automation ??= new UIA3Automation();
                    var window = _automation.FromHandle(hwnd);
                    url = TryGetAddressBarValue(window);
                }
                catch
                {
                    // UIA может падать на закрывающихся окнах, при смене вкладки и т.п. — это норма.
                }

                // Кэш не разрастается: hwnd живут недолго, но на всякий случай чистим старые.
                if (Cache.Count > 64)
                {
                    foreach (var key in Cache.Where(kv => DateTime.Now - kv.Value.At > TimeSpan.FromMinutes(5))
                                 .Select(kv => kv.Key).ToList())
                        Cache.Remove(key);
                }

                Cache[hwnd] = (url, DateTime.Now);
                return url;
            }
        }

        private static string? TryGetAddressBarValue(AutomationElement window)
        {
            // Адресная строка Edge — Edit с ValuePattern. Имя локализовано
            // ("Address and search bar" / «Адресная строка и строка поиска»),
            // поэтому сначала ищем по имени, иначе берём первый Edit со значением.
            var cf = new ConditionFactory(new UIA3PropertyLibrary());
            var edits = window.FindAllDescendants(cf.ByControlType(ControlType.Edit));
            if (edits == null || edits.Length == 0)
                return null;

            AutomationElement? addressBar = edits.FirstOrDefault(e =>
            {
                var name = e.Name;
                return !string.IsNullOrEmpty(name)
                    && (name.Contains("address", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("адрес", StringComparison.OrdinalIgnoreCase));
            }) ?? edits.FirstOrDefault(e => e.Patterns.Value.IsSupported);

            if (addressBar == null || !addressBar.Patterns.Value.IsSupported)
                return null;

            var value = addressBar.Patterns.Value.Pattern.Value.Value;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
