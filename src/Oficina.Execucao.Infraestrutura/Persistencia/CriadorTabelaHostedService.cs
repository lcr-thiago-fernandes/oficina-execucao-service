using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Oficina.Execucao.Infraestrutura.Persistencia;

/// <summary>Cria a tabela quando <see cref="OpcoesDynamoDb.CriarTabelaSeAusente"/> e ela nao existe; aguarda ACTIVE.</summary>
public sealed class CriadorTabelaHostedService : IHostedService
{
    private readonly IAmazonDynamoDB _dynamo;
    private readonly OpcoesDynamoDb _opcoes;

    public CriadorTabelaHostedService(IAmazonDynamoDB dynamo, IOptions<OpcoesDynamoDb> opcoes)
    {
        _dynamo = dynamo;
        _opcoes = opcoes.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_opcoes.CriarTabelaSeAusente) return;

        try
        {
            await _dynamo.DescribeTableAsync(_opcoes.NomeTabela, cancellationToken);
        }
        catch (ResourceNotFoundException)
        {
            try
            {
                await _dynamo.CreateTableAsync(EsquemaTabelaExecucao.CriarRequest(_opcoes.NomeTabela), cancellationToken);
            }
            catch (ResourceInUseException)
            {
                // outra instancia criou ao mesmo tempo
            }
        }

        await AguardarAtivaAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task AguardarAtivaAsync(CancellationToken ct)
    {
        for (var tentativa = 0; tentativa < 60; tentativa++)
        {
            var r = await _dynamo.DescribeTableAsync(_opcoes.NomeTabela, ct);
            var indicesAtivos = r.Table.GlobalSecondaryIndexes?.All(i => i.IndexStatus == IndexStatus.ACTIVE) ?? true;
            if (r.Table.TableStatus == TableStatus.ACTIVE && indicesAtivos) return;
            await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        }

        throw new TimeoutException($"A tabela {_opcoes.NomeTabela} nao ficou ACTIVE a tempo.");
    }
}
