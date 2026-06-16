namespace ManWei.Api.DTOs;

public class WxChatRequestDto
{
    public string Message { get; set; } = string.Empty;
    public List<WxChatMessage> History { get; set; } = new();
}

public class WxChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; set; }
}

public class WxChatResponseDto
{
    public string Answer { get; set; } = string.Empty;
}
