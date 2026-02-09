namespace DeepSeek.Domain
{
    public class ChatRequest
    {
        public string Model { get; set; }
        public List<Promt> Messages { get; set; }
        public bool Stream { get; set; }
    }
}
