using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Messaging;
using Elib.Contracts.Events.Payment;
using Elib.Payment.Application;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Elib.Payment.Infrastructure;

/// <summary>Khoản cần thu từ circulation / patron (chỉ đơn vị có license PAYMENT).</summary>
public sealed class ChargeIssuedConsumer(ChargeIngest ingest, ICrudDbContext db, IModuleLicenseSource licenses, ILogger<ChargeIssuedConsumer> logger)
    : ElibConsumer<ChargeIssued>(licenses, logger)
{
    protected override string? RequiredModule => "PAYMENT";

    protected override async Task HandleAsync(ChargeIssued message, ConsumeContext<ChargeIssued> context)
    {
        await ingest.ApplyAsync(message, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

public sealed class ChargeWaivedConsumer(ChargeIngest ingest, ICrudDbContext db, IModuleLicenseSource licenses, ILogger<ChargeWaivedConsumer> logger)
    : ElibConsumer<ChargeWaived>(licenses, logger)
{
    protected override string? RequiredModule => "PAYMENT";

    protected override async Task HandleAsync(ChargeWaived message, ConsumeContext<ChargeWaived> context)
    {
        await ingest.ApplyAsync(message, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

public sealed class ChargeSettledConsumer(ChargeIngest ingest, ICrudDbContext db, IModuleLicenseSource licenses, ILogger<ChargeSettledConsumer> logger)
    : ElibConsumer<ChargeSettled>(licenses, logger)
{
    protected override string? RequiredModule => "PAYMENT";

    protected override async Task HandleAsync(ChargeSettled message, ConsumeContext<ChargeSettled> context)
    {
        await ingest.ApplyAsync(message, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
