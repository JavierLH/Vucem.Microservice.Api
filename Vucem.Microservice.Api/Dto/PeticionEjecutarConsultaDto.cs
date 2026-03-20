using System;

namespace Vucem.Microservice.Api.Dto
{
    public class PeticionEjecutarConsultaDto
    {
        public string UsuarioWcf { get; set; }
        public string PasswordWcf { get; set; }
        public string RfcSolicitante { get; set; }
        public long NumeroOperacion { get; set; }
        public string PasswordLlave { get; set; }
        public string CertificadoBase64 { get; set; }
        public string LlavePrivadaBase64 { get; set; }
    }
}