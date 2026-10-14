using System.ComponentModel.DataAnnotations;

namespace SkillSwap.Application.DTOs.Conversations;

/// <summary>
/// Request body for sending a new message in a conversation.
/// </summary>
public record SendMessageRequest(
    [property: Required]
    [property: MinLength(1, ErrorMessage = "Message body cannot be empty.")]
    [property: MaxLength(4000, ErrorMessage = "Message body cannot exceed 4000 characters.")]
    string Body);
