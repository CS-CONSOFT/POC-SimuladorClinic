using System;

namespace SimuladorFilaBlazor.Models
{
    public class Consulta
    {
        public int Numero { get; set; }
        public string Paciente { get; set; }
        public string PCD { get; set; }
        public int Peso { get; set; }
        
        // Horário de início calculado dinamicamente (muda conforme recálculo da fila)
        public TimeSpan? HoraInicio { get; set; }
        
        // Horário de término calculado (HoraInicio + duração)
        public TimeSpan? HoraFinal { get; set; }
        
        // Horário fixo para descanso (não muda)
        public TimeSpan? HorarioFixoDescanso { get; set; }
        
        public int Fila { get; set; }
        public string CheckIn { get; set; }
        public string CheckInNoLocal { get; set; }
        public string Localizacao { get; set; }
        public int TempoChegada { get; set; }
        public int TempoExtraMinutos { get; set; }
        public string Desistencia { get; set; }
        public string Tipo { get; set; }
        public string Status { get; set; }
        public double PrioridadeEfetiva { get; set; }
    }

    public class EstadoFila
    {
        public TimeSpan HoraAtual { get; set; }
        public int IncrementoTempo { get; set; }
        public List<Consulta> ListaConsultas { get; set; }
        public List<string> Logs { get; set; } = new();
    }
}