using System;
using System.Collections.Generic;
using System.Linq;
using SimuladorFilaBlazor.Models;

namespace SimuladorFilaBlazor.Services
{
    public class SimuladorService
    {
        private EstadoFila _estado;
        private const int TEMPO_PADRAO_CONSULTA = 30;
        private const int TEMPO_PADRAO_DESCANSO = 15;

        public event Action OnChange;

        public SimuladorService()
        {
            Reset();
        }

        public EstadoFila ObterEstado() => _estado;

        public void Reset()
        {
            _estado = new EstadoFila
            {
                HoraAtual = new TimeSpan(7, 0, 0),
                IncrementoTempo = 15,
                ListaConsultas = new List<Consulta>
                {
                    new Consulta { Numero = 1, Paciente = "João Pedro", PCD = "Deficiência Física", Peso = 8, Fila = 1, CheckIn = "S", CheckInNoLocal = "S", Tipo = "Consulta", Status = "Aberto" },
                    new Consulta { Numero = 2, Paciente = "Maria Cecilia", Peso = 0, Fila = 2, CheckIn = "S", Tipo = "Consulta", Status = "Aberto", TempoChegada = 90 },
                    new Consulta { Numero = 3, Paciente = "Jose Silva", Peso = 0, Fila = 3, CheckIn = "N", Tipo = "Consulta", Status = "Aberto", TempoChegada = 0 }, // SEM CHECK-IN - Vai para o final da fila
                    new Consulta { Numero = 4, Paciente = "Raimunda Gomes", PCD = "Idoso Dependência I", Peso = 5, Fila = 4, CheckIn = "S", CheckInNoLocal = "S", TempoChegada = 120, Tipo = "Consulta", Status = "Aberto" },
                    new Consulta { Numero = 5, Paciente = "Camila Pitanga", Peso = 0, Fila = 5, CheckIn = "S", TempoChegada = 10, Tipo = "Consulta", Status = "Aberto" },
                    new Consulta { Numero = 6, Paciente = "DESCANSO MÉDICO", TempoExtraMinutos = 15, Tipo = "Descanso", Status = "Aberto", Fila = 6, HorarioFixoDescanso = new TimeSpan(10, 0, 0) }, // Descanso fixo às 10:00
                    new Consulta { Numero = 7, Paciente = "Michael Jackson", PCD = "Deficiência Múltipla", Peso = 9, Fila = 7, Tipo = "Consulta", Status = "Aberto", CheckIn = "S", TempoChegada = 120 },
                    new Consulta { Numero = 8, Paciente = "Maria Clara", Fila = 8, CheckIn = "S", Tipo = "Consulta", Status = "Aberto", TempoChegada = 90 },
                    new Consulta { Numero = 9, Paciente = "Castanho Gomes", Peso = 0, Fila = 9, CheckIn = "S", Tipo = "Consulta", Status = "Aberto", TempoChegada = 90 },
                    new Consulta { Numero = 10, Paciente = "Joaquim Última hora", Peso = 0, Fila = 10, CheckIn = "S", Tipo = "Consulta", Status = "Aberto", TempoChegada = 120 }
                }
            };
            RecalcularFila();
            NotificarMudanca();
        }

        public void AvancarTempo(int minutos)
        {
            _estado.HoraAtual = _estado.HoraAtual.Add(TimeSpan.FromMinutes(minutos));
            AdicionarLog($"⏰ Tempo avançado em {minutos} minutos. Hora atual: {_estado.HoraAtual:hh\\:mm}");

            foreach (var consulta in _estado.ListaConsultas.Where(c => c.Tipo == "Consulta" && c.CheckInNoLocal != "S"))
            {
                if (consulta.TempoChegada > 0)
                {
                    consulta.TempoChegada -= minutos;
                    if (consulta.TempoChegada <= 0)
                    {
                        consulta.TempoChegada = 0;
                        consulta.CheckInNoLocal = "S";
                        AdicionarLog($"✅ {consulta.Paciente} chegou ao local!");
                    }
                }
            }

            DetectarTempoExtra();
            DetectarDesistencias();
            RecalcularFila();
            NotificarMudanca();
        }

        public void AlterarCheckInLocal(int numero, bool checkIn)
        {
            var consulta = _estado.ListaConsultas.FirstOrDefault(c => c.Numero == numero);
            if (consulta != null)
            {
                consulta.CheckInNoLocal = checkIn ? "S" : "N";
                if (checkIn) consulta.TempoChegada = 0;
                AdicionarLog($"📍 {consulta.Paciente} - Check-in no local: {(checkIn ? "SIM" : "NÃO")}");
                RecalcularFila();
                NotificarMudanca();
            }
        }

        public void AlterarCheckIn(int numero, bool checkIn)
        {
            var consulta = _estado.ListaConsultas.FirstOrDefault(c => c.Numero == numero);
            if (consulta != null)
            {
                consulta.CheckIn = checkIn ? "S" : "N";
                if (!checkIn)
                {
                    // Se desmarcar check-in, resetar check-in no local também
                    consulta.CheckInNoLocal = "N";
                    consulta.TempoChegada = 0;
                }
                AdicionarLog($"📋 {consulta.Paciente} - Check-in geral: {(checkIn ? "REALIZADO" : "NÃO REALIZADO")}");
                RecalcularFila();
                NotificarMudanca();
            }
        }

        public void AlterarTempoChegada(int numero, int tempoMinutos)
        {
            var consulta = _estado.ListaConsultas.FirstOrDefault(c => c.Numero == numero);
            if (consulta != null)
            {
                consulta.TempoChegada = tempoMinutos;
                if (tempoMinutos == 0) consulta.CheckInNoLocal = "S";
                AdicionarLog($"🚗 {consulta.Paciente} - Tempo de chegada: {tempoMinutos} minutos");
                RecalcularFila();
                NotificarMudanca();
            }
        }

        public void MarcarDesistencia(int numero)
        {
            var consulta = _estado.ListaConsultas.FirstOrDefault(c => c.Numero == numero);
            if (consulta != null)
            {
                consulta.Desistencia = "S";
                consulta.Status = "Desistente";
                AdicionarLog($"❌ {consulta.Paciente} marcado como desistente");
                RecalcularFila();
                NotificarMudanca();
            }
        }

        public void AdicionarTempoExtra(int numero, int tempoMinutos)
        {
            var consulta = _estado.ListaConsultas.FirstOrDefault(c => c.Numero == numero);
            if (consulta != null)
            {
                consulta.TempoExtraMinutos = tempoMinutos;
                var tipo = consulta.Tipo == "Descanso" ? "descanso" : "consulta";
                AdicionarLog($"⏱️ {consulta.Paciente} - Tempo extra de {tipo}: {tempoMinutos} minutos");
                RecalcularFila();
                NotificarMudanca();
            }
        }

        public void IniciarConsulta(int numero)
        {
            var consulta = _estado.ListaConsultas.FirstOrDefault(c => c.Numero == numero);
            if (consulta != null)
            {
                consulta.Status = "Em Atendimento";
                consulta.HoraInicio = _estado.HoraAtual; // Registrar hora de início
                
                if (consulta.Tipo == "Descanso")
                {
                    AdicionarLog($"☕ Descanso médico iniciado às {_estado.HoraAtual:hh\\:mm}");
                }
                else
                {
                    AdicionarLog($"🩺 Consulta iniciada: {consulta.Paciente} às {_estado.HoraAtual:hh\\:mm}");
                }
                RecalcularFila();
                NotificarMudanca();
            }
        }

        public void FinalizarConsulta(int numero)
        {
            var consulta = _estado.ListaConsultas.FirstOrDefault(c => c.Numero == numero);
            if (consulta != null)
            {
                consulta.Status = "Finalizado";
                consulta.HoraFinal = _estado.HoraAtual; // Atualiza hora final para o momento atual
                
                if (consulta.Tipo == "Descanso")
                {
                    AdicionarLog($"✅ Descanso médico finalizado às {_estado.HoraAtual:hh\\:mm}");
                }
                else
                {
                    AdicionarLog($"✅ Consulta finalizada: {consulta.Paciente} às {_estado.HoraAtual:hh\\:mm}");
                }
                
                // Ajustar horário da próxima consulta/descanso para começar no horário atual
                AjustarProximaConsulta();
                
                RecalcularFila();
                NotificarMudanca();
            }
        }

        private void AjustarProximaConsulta()
        {
            // Encontrar a próxima consulta ou descanso na fila (menor posição, que não esteja finalizada ou em atendimento)
            var proximaConsulta = _estado.ListaConsultas
                .Where(c => c.Status == "Aberto" && c.Desistencia != "S")
                .OrderBy(c => c.Fila)
                .FirstOrDefault();

            if (proximaConsulta != null)
            {
                // Se for descanso, sempre pode começar imediatamente
                if (proximaConsulta.Tipo == "Descanso")
                {
                    proximaConsulta.HoraInicio = _estado.HoraAtual.Add(TimeSpan.FromMinutes(1));
                    AdicionarLog($"📅 Próximo descanso ajustado - Início: {proximaConsulta.HoraInicio:hh\\:mm}");
                }
                // Se a próxima consulta está no local, ajusta para começar no horário atual + 1 minuto
                else if (proximaConsulta.CheckInNoLocal == "S")
                {
                    proximaConsulta.HoraInicio = _estado.HoraAtual.Add(TimeSpan.FromMinutes(1));
                    AdicionarLog($"📅 Próxima consulta ajustada: {proximaConsulta.Paciente} - Início: {proximaConsulta.HoraInicio:hh\\:mm}");
                }
                // Se não está no local, verifica quando chegará
                else if (proximaConsulta.TempoChegada > 0)
                {
                    var horaChegada = _estado.HoraAtual.Add(TimeSpan.FromMinutes(proximaConsulta.TempoChegada));
                    var horarioDisponivel = _estado.HoraAtual.Add(TimeSpan.FromMinutes(1));
                    
                    // Usa o maior entre a hora de chegada e o horário disponível
                    proximaConsulta.HoraInicio = horaChegada > horarioDisponivel ? horaChegada : horarioDisponivel;
                    AdicionarLog($"📅 Próxima consulta ajustada: {proximaConsulta.Paciente} - Início: {proximaConsulta.HoraInicio:hh\\:mm} (aguardando chegada)");
                }
            }
        }

        private void DetectarTempoExtra()
        {
            var consultasEmAndamento = _estado.ListaConsultas
                .Where(c => c.Status == "Em Atendimento" && c.HoraInicio.HasValue);

            foreach (var consulta in consultasEmAndamento)
            {
                var tempoDecorrido = (_estado.HoraAtual - consulta.HoraInicio.Value).TotalMinutes;
                
                if (consulta.Tipo == "Descanso")
                {
                    var duracaoBase = consulta.TempoExtraMinutos > 0 ? consulta.TempoExtraMinutos : TEMPO_PADRAO_DESCANSO;
                    
                    if (tempoDecorrido > duracaoBase)
                    {
                        var extraDetectado = (int)(tempoDecorrido - duracaoBase);
                        AdicionarLog($"⚠️ Descanso prolongado: {extraDetectado}min além do previsto");
                    }
                }
                else
                {
                    var tempoPrevisto = TEMPO_PADRAO_CONSULTA + consulta.TempoExtraMinutos;
                    if (tempoDecorrido > tempoPrevisto)
                    {
                        var extraDetectado = (int)(tempoDecorrido - tempoPrevisto);
                        consulta.TempoExtraMinutos = (int)tempoDecorrido - TEMPO_PADRAO_CONSULTA;
                        AdicionarLog($"⚠️ Tempo extra detectado: {consulta.Paciente} - {extraDetectado}min além do previsto");
                    }
                }
            }
        }

        private void DetectarDesistencias()
        {
            foreach (var consulta in _estado.ListaConsultas.Where(c => c.Tipo == "Consulta" && c.Status == "Aberto"))
            {
                if (consulta.HoraFinal.HasValue &&
                    _estado.HoraAtual > consulta.HoraFinal.Value.Add(TimeSpan.FromMinutes(30)) &&
                    consulta.CheckInNoLocal != "S")
                {
                    consulta.Desistencia = "S";
                    consulta.Status = "Desistente (Auto)";
                    AdicionarLog($"⚠️ Desistência automática: {consulta.Paciente}");
                }
            }
        }

        public void RecalcularFila()
        {
            // Calcular prioridade apenas para consultas (não para descansos)
            foreach (var consulta in _estado.ListaConsultas.Where(c => c.Tipo == "Consulta" && c.Desistencia != "S"))
            {
                CalcularPrioridadeEfetiva(consulta);
            }

            // Filtrar apenas itens não finalizados para a fila visual
            var consultasOrdenadas = _estado.ListaConsultas
                .Where(c => c.Desistencia != "S" && c.Status != "Finalizado")
                .OrderByDescending(c => c.PrioridadeEfetiva)
                .ThenBy(c => c.HoraInicio)
                .ToList();

            // Separar consultas que estão no local das que não estão
            var consultasNoLocal = consultasOrdenadas
                .Where(c => c.Tipo == "Consulta" && c.CheckInNoLocal == "S")
                .ToList();

            var consultasAusentes = consultasOrdenadas
                .Where(c => c.Tipo == "Consulta" && c.CheckInNoLocal != "S")
                .ToList();

            var descansos = _estado.ListaConsultas
                .Where(c => c.Tipo == "Descanso" && c.Status != "Finalizado" && c.Desistencia != "S")
                .ToList();

            // Criar lista ordenada: primeiro os presentes, depois os ausentes
            var filaOrdenada = new List<Consulta>();
            
            int indexPresente = 0;
            int indexAusente = 0;

            // Intercalar: sempre priorizar presentes, mas manter ausentes em suas posições relativas
            while (indexPresente < consultasNoLocal.Count || indexAusente < consultasAusentes.Count)
            {
                // Adicionar todos os presentes disponíveis
                while (indexPresente < consultasNoLocal.Count)
                {
                    filaOrdenada.Add(consultasNoLocal[indexPresente]);
                    indexPresente++;
                }

                // Adicionar um ausente (que ficará aguardando chegar)
                if (indexAusente < consultasAusentes.Count)
                {
                    filaOrdenada.Add(consultasAusentes[indexAusente]);
                    indexAusente++;
                }
            }

            // Inserir descansos em posições estratégicas (após cada N consultas ou em horário específico)
            var filaComDescanso = new List<Consulta>();
            int consultasAntes = 5; // Número de consultas antes do descanso
            
            for (int i = 0; i < filaOrdenada.Count; i++)
            {
                filaComDescanso.Add(filaOrdenada[i]);
                
                // Inserir descanso após N consultas
                if ((i + 1) == consultasAntes && descansos.Any())
                {
                    var descanso = descansos.First();
                    filaComDescanso.Add(descanso);
                    descansos.Remove(descanso);
                }
            }
            
            // Adicionar descansos restantes ao final
            filaComDescanso.AddRange(descansos);

            // Atribuir posições na fila (apenas para itens não finalizados)
            int posicaoFila = 1;
            foreach (var consulta in filaComDescanso)
            {
                consulta.Fila = posicaoFila++;
            }

            // Calcular horários em cascata para TODOS os itens (incluindo finalizados para histórico)
            CalcularHorariosCascata(filaComDescanso);
        }

        private void CalcularPrioridadeEfetiva(Consulta consulta)
        {
            double prioridade = consulta.Peso * 10;

            // PENALIZAR FORTEMENTE quem NÃO fez check-in - vai para o FINAL da fila
            if (consulta.CheckIn != "S")
            {
                prioridade -= 5000; // Penalidade muito alta - vai para o final
                AdicionarLog($"⚠️ {consulta.Paciente} sem check-in - movido para o final da fila");
            }

            // Priorizar quem está no local
            if (consulta.CheckInNoLocal == "S")
            {
                prioridade += 50;
            }

            // Penalizar quem está longe
            if (consulta.TempoChegada > 30)
            {
                prioridade -= (consulta.TempoChegada - 30) * 0.3;
            }

            // Bônus para quem está em atendimento
            if (consulta.Status == "Em Atendimento")
            {
                prioridade += 10000; // Sempre primeiro
            }

            consulta.PrioridadeEfetiva = prioridade;
        }

        private void CalcularHorariosCascata(List<Consulta> consultasOrdenadas)
        {
            var horaCorrente = _estado.HoraAtual;

            // Se houver consulta ou descanso em atendimento, começar após ela
            var itemEmAtendimento = consultasOrdenadas.FirstOrDefault(c => c.Status == "Em Atendimento");
            if (itemEmAtendimento != null && itemEmAtendimento.HoraInicio.HasValue)
            {
                var duracao = itemEmAtendimento.Tipo == "Descanso" 
                    ? (itemEmAtendimento.TempoExtraMinutos > 0 ? itemEmAtendimento.TempoExtraMinutos : TEMPO_PADRAO_DESCANSO)
                    : (TEMPO_PADRAO_CONSULTA + itemEmAtendimento.TempoExtraMinutos);
                
                var horaFimPrevista = itemEmAtendimento.HoraInicio.Value.Add(TimeSpan.FromMinutes(duracao));
                
                // Se a hora prevista de fim é futura, usar ela
                if (horaFimPrevista > _estado.HoraAtual)
                {
                    horaCorrente = horaFimPrevista.Add(TimeSpan.FromMinutes(1));
                }
            }

            foreach (var consulta in consultasOrdenadas)
            {
                // Pular consultas finalizadas
                if (consulta.Status == "Finalizado")
                {
                    continue;
                }

                if (consulta.Tipo == "Descanso")
                {
                    // Se já está em atendimento, não recalcular hora de início
                    if (consulta.Status == "Em Atendimento")
                    {
                        // Hora de início já está definida, não alterar
                        // Calcular apenas hora final baseada na duração
                        var duracaoDescanso = consulta.TempoExtraMinutos > 0 ? consulta.TempoExtraMinutos : TEMPO_PADRAO_DESCANSO;
                        consulta.HoraFinal = consulta.HoraInicio.Value.Add(TimeSpan.FromMinutes(duracaoDescanso));
                        horaCorrente = consulta.HoraFinal.Value.Add(TimeSpan.FromMinutes(1));
                    }
                    else
                    {
                        // Descanso com horário fixo definido
                        if (consulta.HorarioFixoDescanso.HasValue)
                        {
                            consulta.HoraInicio = consulta.HorarioFixoDescanso.Value;
                        }
                        else
                        {
                            // Se não tem horário fixo, usar o horário corrente
                            consulta.HoraInicio = horaCorrente;
                        }
                        
                        var duracaoDescanso = consulta.TempoExtraMinutos > 0 ? consulta.TempoExtraMinutos : TEMPO_PADRAO_DESCANSO;
                        consulta.HoraFinal = consulta.HoraInicio.Value.Add(TimeSpan.FromMinutes(duracaoDescanso));
                        horaCorrente = consulta.HoraFinal.Value.Add(TimeSpan.FromMinutes(1));
                    }
                }
                else if (consulta.Tipo == "Consulta")
                {
                    TimeSpan horaInicioConsulta;

                    // Se já está em atendimento, não recalcular hora de início
                    if (consulta.Status == "Em Atendimento")
                    {
                        // Hora de início já está definida, não alterar
                        horaInicioConsulta = consulta.HoraInicio.Value;
                        var duracaoConsulta = TEMPO_PADRAO_CONSULTA + consulta.TempoExtraMinutos;
                        consulta.HoraFinal = horaInicioConsulta.Add(TimeSpan.FromMinutes(duracaoConsulta));
                        
                        // Próxima consulta começa após esta
                        horaCorrente = consulta.HoraFinal.Value.Add(TimeSpan.FromMinutes(1));
                    }
                    // Se está no local ou tempo de chegada é zero
                    else if (consulta.CheckInNoLocal == "S" || consulta.TempoChegada == 0)
                    {
                        horaInicioConsulta = horaCorrente;
                        consulta.HoraInicio = horaInicioConsulta;
                        
                        var duracaoConsulta = TEMPO_PADRAO_CONSULTA + consulta.TempoExtraMinutos;
                        consulta.HoraFinal = horaInicioConsulta.Add(TimeSpan.FromMinutes(duracaoConsulta));
                        horaCorrente = consulta.HoraFinal.Value.Add(TimeSpan.FromMinutes(1));
                    }
                    // Se tem tempo de chegada
                    else if (consulta.TempoChegada > 0)
                    {
                        var horaChegadaPaciente = _estado.HoraAtual.Add(TimeSpan.FromMinutes(consulta.TempoChegada));
                        horaInicioConsulta = horaCorrente > horaChegadaPaciente ? horaCorrente : horaChegadaPaciente;
                        consulta.HoraInicio = horaInicioConsulta;
                        
                        var duracaoConsulta = TEMPO_PADRAO_CONSULTA + consulta.TempoExtraMinutos;
                        consulta.HoraFinal = horaInicioConsulta.Add(TimeSpan.FromMinutes(duracaoConsulta));
                        horaCorrente = consulta.HoraFinal.Value.Add(TimeSpan.FromMinutes(1));
                    }
                    else
                    {
                        horaInicioConsulta = horaCorrente;
                        consulta.HoraInicio = horaInicioConsulta;
                        
                        var duracaoConsulta = TEMPO_PADRAO_CONSULTA + consulta.TempoExtraMinutos;
                        consulta.HoraFinal = horaInicioConsulta.Add(TimeSpan.FromMinutes(duracaoConsulta));
                        horaCorrente = consulta.HoraFinal.Value.Add(TimeSpan.FromMinutes(1));
                    }
                }
            }
        }

        private void AdicionarLog(string mensagem)
        {
            _estado.Logs.Insert(0, $"[{_estado.HoraAtual:hh\\:mm}] {mensagem}");
            if (_estado.Logs.Count > 50) _estado.Logs.RemoveAt(_estado.Logs.Count - 1);
        }

        private void NotificarMudanca()
        {
            OnChange?.Invoke();
        }
    }
}