namespace SkillSwap.Application.DTOs.Conversations;

/// <summary>
/// Represents a conversation summary returned from the API.
/// </summary>
public record ConversationDto(
    long Id,
    long SwapRequestId,
    IReadOnlyList<Guid> ParticipantIds,
    int UnreadCount,
    DateTime CreatedAtUtc,
    MessageDto? LastMessage);
