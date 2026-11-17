using Oficina.Execucao.Api.Testes;
using Reqnroll;

namespace Oficina.Execucao.Bdd.Testes.Suporte;

[Binding]
public sealed class Hooks(AmbienteBdd ambiente)
{
    [BeforeTestRun]
    public static async Task IniciarContainer()
    {
        AmbienteBdd.Dynamo = new DynamoDbApiFixture();
        await AmbienteBdd.Dynamo.InitializeAsync();
    }

    [AfterTestRun]
    public static async Task EncerrarContainer()
    {
        if (AmbienteBdd.Dynamo is not null) await AmbienteBdd.Dynamo.DisposeAsync();
    }

    [AfterScenario]
    public async Task EncerrarFactory() => await ambiente.DisposeAsync();
}
