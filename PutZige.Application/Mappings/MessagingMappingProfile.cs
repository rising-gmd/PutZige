using AutoMapper;
using PutZige.Domain.Entities;
using PutZige.Application.DTOs.Messaging;

namespace PutZige.Application.Mappings;

public class MessagingMappingProfile : Profile
{
    public MessagingMappingProfile()
    {
        CreateMap<Message, MessageDto>()
            .ForMember(d => d.SenderUsername, opt => opt.MapFrom(s => s.Sender != null ? s.Sender.Username : string.Empty))
            .ForMember(d => d.ReceiverUsername, opt => opt.MapFrom(s => s.Receiver != null ? s.Receiver.Username : string.Empty));

        CreateMap<Message, SendMessageResponse>()
            .ConstructUsing(s => new SendMessageResponse
            {
                MessageId = s.Id,
                ConversationId = s.ConversationId ?? Guid.Empty,
                SenderId = s.SenderId,
                ReceiverId = s.ReceiverId,
                MessageText = s.MessageText,
                SentAt = s.SentAt
            });
    }
}