using System.Reflection;
using FluentAssertions;

namespace Oficina.Execucao.Infraestrutura.Testes;

/// <summary>
/// No EKS as credenciais vem do IRSA (AWS_ROLE_ARN + AWS_WEB_IDENTITY_TOKEN_FILE). O AWS SDK
/// so troca o token por credenciais se encontrar o assembly AWSSDK.SecurityToken em tempo de
/// execucao (carregado por NOME, via reflexao): sem o pacote, o pod cai na role do no e toma
/// AccessDenied no DynamoDB/SQS/SNS. Por isso o carregamento e por nome, nao por typeof:
/// se o PackageReference sumir da Infraestrutura, este teste falha em vez de nao compilar.
/// </summary>
public class CredenciaisIrsaTestes
{
    [Fact]
    public void AwsSdkSecurityToken_EstaDisponivelParaOProvedorDeWebIdentity()
    {
        var assembly = Assembly.Load(new AssemblyName("AWSSDK.SecurityToken"));

        var cliente = assembly.GetType("Amazon.SecurityToken.AmazonSecurityTokenServiceClient");

        cliente.Should().NotBeNull();
        assembly.GetName().Version!.Major.Should().Be(4, "AWSSDK v4 em todo o repositorio");
    }
}
