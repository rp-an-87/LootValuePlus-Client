namespace LootValuePlus
{

    internal static class TooltipUtils
    {

        // TODO: add a method that adds a warning ( parenthesis with size 11 and color red )

        public static void AppendFullLineToTooltip(ref string tooltipText, string text, int? size, string color)
        {
            if (size.HasValue)
            {
                StartSizeTag(ref tooltipText, size.Value);
            }
            AppendNewLineToTooltipText(ref tooltipText);
            AppendTextToToolip(ref tooltipText, text, color);
            if (size.HasValue)
            {
                EndSizeTag(ref tooltipText);
            }
        }

        public static void AppendNewLineToTooltipText(ref string tooltipText)
        {
            tooltipText += $"<br>";
        }

        public static void AppendTextToToolip(ref string tooltipText, string addText, string color)
        {
            tooltipText += $"<color={color}>{addText}</color>";
        }

        public static void StartSizeTag(ref string tooltipText, int size)
        {
            tooltipText += $"<size={size}>";
        }

        public static void EndSizeTag(ref string tooltipText)
        {
            tooltipText += $"</size>";
        }

        // A thin line of em dashes, about 170 pixels wide: as wide as the narrowest tooltips already are, so it doesn't widen any
        public static void AppendSeparator(ref string tooltipText, bool appendNewLineAfter = true)
        {
            AppendNewLineToTooltipText(ref tooltipText);
            tooltipText += $"<size=7><color=#555555>{new string('—', 48)}</color></size>";
            if (appendNewLineAfter)
            {
                AppendNewLineToTooltipText(ref tooltipText);
            }
        }

        // "[Alt+Shift+Click] sell": the keys in light grey brackets, then what they do, small like the hotkey hints of CustomRpTweaks
        public static void AppendHotkey(ref string tooltipText, string keys, string action)
        {
            tooltipText += $"<size=10><color=#B8B8B8>[{keys}]</color> <color=#8A8A8A>{action}</color></size>";
        }

    }

}