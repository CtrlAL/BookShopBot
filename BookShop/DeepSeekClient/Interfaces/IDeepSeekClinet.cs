namespace DeepSeek.Interfaces
{
    public interface IDeepSeekClinet
    {
        public Task<string> SendRequest(string prompt);
    }
}
