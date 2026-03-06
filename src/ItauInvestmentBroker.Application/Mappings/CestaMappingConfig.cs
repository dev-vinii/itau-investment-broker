using ItauInvestmentBroker.Application.DTOs.Cesta;
using ItauInvestmentBroker.Domain.Entities;
using Mapster;

namespace ItauInvestmentBroker.Application.Mappings;

public class CestaMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Cesta, CestaResponse>()
            .Map(dest => dest.CestaId, src => src.Id);

        config.NewConfig<ItemCesta, ItemCestaResponse>();

        config.NewConfig<CestaRequest, Cesta>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.Ativa)
            .Ignore(dest => dest.DataCriacao)
            .Ignore(dest => dest.DataDesativacao!)
            .Ignore(dest => dest.CreatedAt)
            .Ignore(dest => dest.UpdatedAt);

        config.NewConfig<ItemCestaRequest, ItemCesta>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.CestaId)
            .Ignore(dest => dest.Cesta!)
            .Ignore(dest => dest.CreatedAt)
            .Ignore(dest => dest.UpdatedAt);
    }
}
