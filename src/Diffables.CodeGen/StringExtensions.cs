namespace Diffables.CodeGen
{
    internal static class StringExtensions
    {
        internal static string WithLowerFirstChar(this string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            return char.ToLower(text[0]) + text.Substring(1);
        }
    }
}
