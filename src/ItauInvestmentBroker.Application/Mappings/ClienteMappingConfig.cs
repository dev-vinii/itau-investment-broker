using ItauInvestmentBroker.Application.DTOs.Cliente;
using ItauInvestmentBroker.Domain.Entities;
using Mapster;

namespace ItauInvestmentBroker.Application.Mappings;

public class ClienteMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<AdesaoRequest, Cliente>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.Ativo)
            .Ignore(dest => dest.DataAdesao)
            .Ignore(dest => dest.ContaGrafica!)
            .Ignore(dest => dest.CreatedAt)
            .Ignore(dest => dest.UpdatedAt);
    }
}
