namespace ManWei.Api.DTOs;

public class ChatRequestDto
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessage> History { get; set; } = new();
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; set; }
}

public class ChatResponseDto
{
    public string Answer { get; set; } = string.Empty;
    public List<DataResultItem>? DataResults { get; set; }
    public string DisplayType { get; set; } = "text";
    public string? ChartType { get; set; }
}

public class DataResultItem : Dictionary<string, object?>
{
    public DataResultItem() { }
    public DataResultItem(IDictionary<string, object?> dictionary) : base(dictionary) { }
}
