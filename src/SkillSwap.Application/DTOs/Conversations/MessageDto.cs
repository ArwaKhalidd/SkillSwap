namespace SkillSwap.Application.DTOs.Conversations;

/// <summary>
/// Represents a single chat message.
/// </summary>
public record MessageDto(
    long Id,
    long ConversationId,
    Guid SenderId,
    string Body,
    DateTime SentAtUtc);
