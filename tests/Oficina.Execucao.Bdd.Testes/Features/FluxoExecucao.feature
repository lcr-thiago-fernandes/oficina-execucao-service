# language: pt
Funcionalidade: Fila de execução da ordem de serviço
  Como mecânico da oficina
  Quero receber as OS pagas numa fila por unidade e registrar diagnóstico, reparo e finalização
  Para que o OS Service acompanhe a execução pela saga

  Cenário: OS percorre a fila até a finalização e libera a vaga
    Dado que a unidade "SP-01" aceita no máximo 1 OS ativa
    Quando o OS Service enviar IniciarExecucao para a OS 1042 da unidade "SP-01"
    Então o evento ExecucaoEnfileirada é publicado com posição 1
    Quando o mecânico registrar o diagnóstico com a observação "Veiculo no elevador 2"
    Então o evento StatusExecucaoAlterado é publicado com status "EmDiagnostico"
    Quando o mecânico registrar o reparo
    Então o evento StatusExecucaoAlterado é publicado com status "EmReparo"
    Quando o mecânico registrar a finalização com a observação "Servico concluido sem pendencias"
    Então o evento ExecucaoFinalizada é publicado
    E a execução da OS 1042 consta como "Finalizada"
    Quando o OS Service enviar IniciarExecucao para a OS 1043 da unidade "SP-01"
    Então o evento ExecucaoEnfileirada é publicado para a OS 1043

  Cenário: Unidade sem capacidade recusa a execução
    Dado que a unidade "SP-01" aceita no máximo 0 OS ativa
    Quando o OS Service enviar IniciarExecucao para a OS 1042 da unidade "SP-01"
    Então o evento ExecucaoRecusada é publicado com o motivo "Capacidade da unidade SP-01 esgotada (0/0)"
    E nenhum evento ExecucaoEnfileirada é publicado

  Cenário: Comando duplicado não ocupa duas vagas
    Dado que a unidade "SP-01" aceita no máximo 1 OS ativa
    Quando o OS Service enviar IniciarExecucao para a OS 1042 da unidade "SP-01" duas vezes
    Então o evento ExecucaoEnfileirada é publicado 2 vezes para a OS 1042
    Quando o OS Service enviar IniciarExecucao para a OS 1043 da unidade "SP-01"
    Então o evento ExecucaoRecusada é publicado para a OS 1043
