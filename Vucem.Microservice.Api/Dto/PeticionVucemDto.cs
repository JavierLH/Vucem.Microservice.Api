using System;
using System.Collections.Generic;

namespace Vucem.Microservice.Api.Dto
{
    public class PeticionVucemDto
    {
        // Credenciales SAT
        public string UsuarioWcf { get; set; }
        public string PasswordWcf { get; set; }
        public string CertificadoBase64 { get; set; }
        public string LlavePrivadaBase64 { get; set; }
        public string PasswordLlave { get; set; }
        public string CadenaOriginal { get; set; }

        // Datos Generales
        public string RfcImportador { get; set; }
        public string Referencia { get; set; }
        public string NumeroPedimento { get; set; }
        public string TipoOperacion { get; set; }
        public bool ExisteVinculacion { get; set; }

        // Totales
        public decimal TotalValorAduana { get; set; }
        public decimal TotalPrecioPagado { get; set; }
        public decimal TotalPrecioPorPagar { get; set; }
        public decimal TotalIncrementables { get; set; }
        public decimal TotalDecrementables { get; set; }

        // Listas Relacionales
        public List<PersonaConsultaVucemDto> PersonasConsulta { get; set; }
        public List<CoveVucemDto> Coves { get; set; }
    }

    public class PersonaConsultaVucemDto
    {
        public string RfcConsulta { get; set; }
        public string TipoFigura { get; set; }
    }

    public class CoveVucemDto
    {
        public string NumeroCove { get; set; }
        public string Incoterm { get; set; }
        public bool ExisteVinculacion { get; set; }
        public string NumeroPedimento { get; set; }
        public string Aduana { get; set; }
        public string Patente { get; set; }
        public string MetodoValoracion { get; set; }

        public List<PagoVucemDto> PreciosPagados { get; set; }
        public List<PagoVucemDto> PreciosPorPagar { get; set; }
        public List<CompensacionVucemDto> Compensaciones { get; set; }
        public List<ConceptoVucemDto> Incrementables { get; set; }
        public List<ConceptoVucemDto> Decrementables { get; set; }
    }

    public class PagoVucemDto
    {
        public DateTime? FechaPago { get; set; }
        public decimal Total { get; set; }
        public string TipoPago { get; set; }
        public string TipoMoneda { get; set; }
        public decimal TipoCambio { get; set; }
        public string SituacionNoFechaPago { get; set; }
        public string DescripcionOtroPago { get; set; }
    }

    public class CompensacionVucemDto
    {
        public DateTime? Fecha { get; set; }
        public string Motivo { get; set; }
        public string PrestacionMercancia { get; set; }
        public string TipoPago { get; set; }
        public string DescripcionOtroPago { get; set; }
    }

    public class ConceptoVucemDto
    {
        public string ClaveConcepto { get; set; }
        public DateTime? FechaErogacion { get; set; }
        public decimal Importe { get; set; }
        public string TipoMoneda { get; set; }
        public decimal TipoCambio { get; set; }
        public bool AcargoImportador { get; set; }
    }
}