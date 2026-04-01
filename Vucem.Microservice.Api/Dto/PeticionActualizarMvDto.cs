using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;


namespace Vucem.Microservice.Api.Dto
{
    public class PeticionActualizarMvDto
    {
        // Credenciales SAT y WCF
        public string UsuarioWcf { get; set; }
        public string PasswordWcf { get; set; }
        public string CertificadoBase64 { get; set; }
        public string LlavePrivadaBase64 { get; set; }
        public string PasswordLlave { get; set; }

        // Datos del Trámite
        public string NumeroMV { get; set; } // El que nos dio el paso 1 (Ej. 136211 o MNVA...)
        public List<string> Edocuments { get; set; } // Solo los números de e-Document
        public List<PersonaConsultaActualizarDto> PersonasConsulta { get; set; }
    }

    public class PersonaConsultaActualizarDto
    {
        public string RfcConsulta { get; set; }
        public string TipoFigura { get; set; }
    }
}