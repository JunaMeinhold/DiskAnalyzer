namespace DiskAnalyzer
{
    public struct TextSpan
    {
        public int Start;
        public int Length;

        public TextSpan(int start, int length)
        {
            Start = start;
            Length = length;
        }

        public TextSpan(int start, string text)
        {
            Start = start;
            Length = text.Length - start;
        }

        public readonly ReadOnlySpan<char> ToSpan(string text)
        {
            return text.AsSpan(Start, Length);
        }
    }
}