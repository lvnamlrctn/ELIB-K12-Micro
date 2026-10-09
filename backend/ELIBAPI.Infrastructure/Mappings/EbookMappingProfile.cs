using AutoMapper;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;

namespace ELIBAPI.Infrastructure.Mappings;

public class EbookMappingProfile : Profile
{
    public EbookMappingProfile()
    {
        CreateMap<EbookItem, PublicEbookResponse>()
            .ForMember(d => d.Title, opt => opt.MapFrom(s => s.ItemXml != null ? s.ItemXml.Title : null))
            .ForMember(d => d.Author, opt => opt.MapFrom(s => s.ItemXml != null ? s.ItemXml.Author : null))
            .ForMember(d => d.Publisher, opt => opt.MapFrom(s => s.ItemXml != null ? s.ItemXml.Publisher : null))
            .ForMember(d => d.PublishDate, opt => opt.MapFrom(s => s.ItemXml != null ? s.ItemXml.PublishDate : null))
            .ForMember(d => d.Keyword, opt => opt.MapFrom(s => s.ItemXml != null ? s.ItemXml.Keyword : null))
            .ForMember(d => d.Xml, opt => opt.MapFrom(s => s.ItemXml != null ? s.ItemXml.Xml : null))
            .ForMember(d => d.OtherTitle, opt => opt.MapFrom(s => s.ItemXml != null ? s.ItemXml.OtherTitle : null))
            .ForMember(d => d.Page, opt => opt.MapFrom(s => s.ItemXml != null ? s.ItemXml.Page : null))
            .ForMember(d => d.CollectionName, opt => opt.MapFrom(s => s.Collection != null ? s.Collection.Name : null))
            .ForMember(d => d.SubjectName, opt => opt.MapFrom(s => s.Subject != null ? s.Subject.Name : null))
            .ForMember(d => d.ToPicName, opt => opt.MapFrom(s => s.Topic != null ? s.Topic.Name : null))

            .ForMember(d => d.Status, opt => opt.MapFrom(s => (int?)s.Status))
            .ForMember(d => d.IsFree, opt => opt.MapFrom(s => (int?)s.Free));

        CreateMap<EbookFile, PublicEbookFileResponse>();
    }
}
