public class ChatMessage
{
    public string type { get; set; }   // "msg"|"join"|"leave"|"pm"|"sys"
    public string from { get; set; }
    public string to { get; set; }     // for pm, else null/empty
    public string text { get; set; }
    public long ts { get; set; }       // unix epoch seconds
}
