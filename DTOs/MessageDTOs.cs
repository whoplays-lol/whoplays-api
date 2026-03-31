using System.ComponentModel.DataAnnotations;
using WannaFill.API.Models;

namespace WannaFill.API.DTOs;

public class SendMessageDto
{
    [Required, MaxLength(100)]
    public string SessionId { get; set; } = null!;

    [Required, MaxLength(500)]
    public string Content { get; set; } = null!;
}

public class MessageDto
{
    public string Id { get; set; } = null!;
    public string MatchGroupId { get; set; } = null!;
    public string Alias { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime SentAt { get; set; }

    public static MessageDto From(TemporaryMessage m) => new()
    {
        Id = m.Id.ToString(),
        MatchGroupId = m.MatchGroupId.ToString(),
        Alias = m.Alias,
        Content = m.Content,
        SentAt = m.SentAt
    };
}
