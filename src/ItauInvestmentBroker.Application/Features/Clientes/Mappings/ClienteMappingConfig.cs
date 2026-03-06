using ItauInvestmentBroker.Application.Features.Clientes.DTOs;
using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Motor.Entities;
using Mapster;

namespace ItauInvestmentBroker.Application.Features.Clientes.Mappings;

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
