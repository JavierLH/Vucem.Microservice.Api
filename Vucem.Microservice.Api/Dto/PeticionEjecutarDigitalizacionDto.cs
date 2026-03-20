using System;

namespace Vucem.Microservice.Api.Dto
{
    public class PeticionEjecutarDigitalizacionDto
    {
        public string UsuarioWcf { get; set; }
        public string PasswordWcf { get; set; }
        public string RfcSolicitante { get; set; }
        public string Correo { get; set; }
        public int IdTipoDocumento { get; set; }
        public string NombreDocumento { get; set; }
        public string RfcConsulta { get; set; }
        public string PasswordLlave { get; set; }
        public string CertificadoBase64 { get; set; }
        public string LlavePrivadaBase64 { get; set; }
        public string ArchivoPdfBase64 { get; set; }
    }
}